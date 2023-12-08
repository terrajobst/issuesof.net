using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

using IssueDb;

using Markdig;

namespace IssuesOfDotNet.Crawler;

internal static class AreaOwnershipLoader
{
    private const string AreaOwnersPath = "docs/area-owners.md";

    public static async Task<CrawledAreaOwnership> ExpandTeamsAsync(GitHubAppClient client, CrawledAreaOwnership ownership)
    {
        var expandedTeams = new Dictionary<string, ParsedTeam>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ownership.Entries)
        {
            AddTeams(expandedTeams, entry.Leads);
            AddTeams(expandedTeams, entry.Owners);
        }

        if (expandedTeams.Count == 0)
            return ownership;

        // Load teams

        foreach (var teamsByOrg in expandedTeams.Values.ToArray().GroupBy(p => p.OrgName))
        {
            var orgName = teamsByOrg.Key;

            await client.UseInstallationTokenAsync(orgName);

            var teams = await client.InvokeAsync(c => c.Organization.Team.GetAll(orgName));
            var teamIdByName = teams.ToDictionary(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var parsedTeam in teamsByOrg)
            {
                if (!teamIdByName.TryGetValue(parsedTeam.TeamName, out var teamId))
                    continue;

                var members = await client.InvokeAsync(c => c.Organization.Team.GetAllMembers(teamId));
                parsedTeam.Members = members.Select(m => m.Login).ToArray();
            }
        }

        // Expand teams

        var entries = new List<CrawledAreaEntry>();

        foreach (var entry in ownership.Entries)
        {
            var label = entry.Label;
            var area = entry.Area;
            var expandedLeads = ExpandMembers(expandedTeams, entry.Leads);
            var expandedOwners = ExpandMembers(expandedTeams, entry.Owners);
            var expandedEntry = new CrawledAreaEntry(label, area, expandedLeads, expandedOwners);
            entries.Add(expandedEntry);
        }

        return new CrawledAreaOwnership(entries.ToArray());

        static void AddTeams(Dictionary<string, ParsedTeam> receiver, IEnumerable<CrawledAreaMember> members)
        {
            foreach (var member in members)
            {
                var qualifiedName = member.UserName;
                if (!receiver.ContainsKey(qualifiedName) &&
                    TryParseTeam(qualifiedName, out var t))
                {
                    var parsedTeam = new ParsedTeam(qualifiedName, t.OrgName, t.TeamName);
                    receiver.Add(member.UserName, parsedTeam);
                }
            }
        }

        static bool TryParseTeam(string text, out (string OrgName, string TeamName) result)
        {
            var positionOfSlash = text.IndexOf('/');
            if (positionOfSlash < 0)
            {
                result = default;
                return false;
            }

            var orgName = text.Substring(0, positionOfSlash);
            var teamName = text.Substring(positionOfSlash + 1);
            result = (orgName, teamName);
            return true;
        }

        static CrawledAreaMember[] ExpandMembers(Dictionary<string, ParsedTeam> expandedTeams, IEnumerable<CrawledAreaMember> members)
        {
            var expandedMembersByName = new Dictionary<string, CrawledAreaMember>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in members)
                expandedMembersByName.Add(member.UserName, member);

            foreach (var member in members)
            {
                if (expandedTeams.TryGetValue(member.UserName, out var parsedTeam))
                {
                    foreach (var teamMember in parsedTeam.Members)
                    {
                        var expandedOrigin = parsedTeam.Origin.Merge(member.Origin);
                        var expandedMember = new CrawledAreaMember(expandedOrigin, teamMember);

                        if (expandedMembersByName.TryGetValue(expandedMember.UserName, out var existingMember))
                        {
                            var mergedOrigin = existingMember.Origin.Merge(expandedMember.Origin);
                            var mergedMember = new CrawledAreaMember(mergedOrigin, expandedMember.UserName);
                            expandedMembersByName[expandedMember.UserName] = mergedMember;
                        }
                        else
                        {
                            expandedMembersByName.Add(expandedMember.UserName, expandedMember);
                        }
                    }
                }
            }

            return expandedMembersByName.Values.OrderBy(m => m.UserName).ToArray();
        }
    }

    public static async Task<CrawledAreaOwnership> FromTeamsAsync(GitHubAppClient client, string orgName)
    {
        await client.UseInstallationTokenAsync(orgName);

        var teams = await client.InvokeAsync(c => c.Organization.Team.GetAll(orgName));
        var entries = new List<CrawledAreaEntry>();

        foreach (var team in teams)
        {
            var label = team.Name;
            if (!TryParseArea(label, out var area))
                continue;

            var teamMembers = await client.InvokeAsync(c => c.Organization.Team.GetAllMembers(team.Id));

            var origin = new CrawledAreaMemberOrigin.Team(orgName, team.Name);
            var leads = Array.Empty<CrawledAreaMember>();
            var owners = teamMembers.Select(m => new CrawledAreaMember(origin, m.Login)).ToArray();
            var entry = new CrawledAreaEntry(label, area, leads, owners);
            entries.Add(entry);
        }

        return new CrawledAreaOwnership(entries.ToArray());
    }

    public static async Task<CrawledAreaOwnership?> FromRepoAsync(string orgName, string repoName)
    {
        var maxTries = 3;
        var url = $"https://raw.githubusercontent.com/{orgName}/{repoName}/main/{AreaOwnersPath}";
        var client = new HttpClient();

        while (maxTries-- > 0)
        {

            try
            {
                var contents = await client.GetStringAsync(url);
                return Parse(orgName, repoName, AreaOwnersPath, contents);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Neither the JSON nor MD files could be found; there's no area owner file
                return null;
            }
            catch (HttpRequestException ex)
            {
                // This might be a transient error.
                Debug.WriteLine(ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        return null;
    }

    private static CrawledAreaOwnership Parse(string orgName, string repoName, string path, string contents)
    {
        var lines = GetLines(contents);
        var entries = new List<CrawledAreaEntry>();

        var hasSeenHeaders = false;
        var indexOfLabel = -1;
        var indexOfLead = -1;
        var indexOfOwners = -1;

        foreach (var (line, lineIndex) in lines.Select((l, i) => (l, i)))
        {
            var parts = line.Split('|');
            if (parts.Length != 6)
                continue;

            var cells = new string[]
            {
                Markdown.ToPlainText(parts[1]).Trim(),
                Markdown.ToPlainText(parts[2]).Trim(),
                Markdown.ToPlainText(parts[3]).Trim()
            };

            if (!hasSeenHeaders)
            {
                Span<string> labelHeaders = ["Area", "Operating System", "Architecture"];

                foreach (var labelHeader in labelHeaders)
                {
                    indexOfLabel = Array.FindIndex(cells, x => x.Contains(labelHeader, StringComparison.OrdinalIgnoreCase));
                    if (indexOfLabel >= 0)
                        break;
                }

                indexOfLead = Array.FindIndex(cells, x => x.Contains("Lead", StringComparison.OrdinalIgnoreCase));
                indexOfOwners = Array.FindIndex(cells, x => x.Contains("Owner", StringComparison.OrdinalIgnoreCase));
                hasSeenHeaders = true;
            }

            if (indexOfLabel < 0 || indexOfLabel >= cells.Length ||
                indexOfLead < 0 || indexOfLead >= cells.Length ||
                indexOfOwners < 0 || indexOfOwners >= cells.Length)
                continue;

            var labelText = cells[indexOfLabel];
            var leadText = cells[indexOfLead];
            var ownerText = cells[indexOfOwners];

            if (!TryParseArea(labelText, out var area))
                continue;

            var lineNumber = lineIndex + 1;
            var origin = new CrawledAreaMemberOrigin.File(orgName, repoName, path, lineNumber);

            var leads = GetUntaggedUserNames(leadText, origin);
            var owners = GetUntaggedUserNames(ownerText, origin);

            var entry = new CrawledAreaEntry(labelText, area, leads, owners);
            entries.Add(entry);
        }

        return new CrawledAreaOwnership(entries.ToArray());

        static CrawledAreaMember[] GetUntaggedUserNames(string text, CrawledAreaMemberOrigin origin)
        {
            var separators = new[] { ' ', ',' };
            return text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => GetUntaggedUserName(t.Trim()))
                       .Select(u => new CrawledAreaMember(origin, u))
                       .ToArray();
        }

        static string GetUntaggedUserName(string userName)
        {
            return userName.StartsWith("@") ? userName[1..] : userName;
        }

        static IEnumerable<string> GetLines(string text)
        {
            using var stringReader = new StringReader(text);
            while (true)
            {
                var line = stringReader.ReadLine();
                if (line == null)
                    yield break;

                yield return line;
            }
        }
    }

    private static bool TryParseArea(string label, [MaybeNullWhen(false)] out string area)
    {
        Span<string> prefixes = ["area-", "os-", "arch-"];

        if (label is not null)
        {
            foreach (var prefix in prefixes)
            {
                if (label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    area = label[prefix.Length..];
                    return true;
                }
            }
        }

        area = null;
        return false;
    }

    private sealed class ParsedTeam
    {
        public ParsedTeam(string qualifiedName, string orgName, string teamName)
        {
            QualifiedName = qualifiedName;
            OrgName = orgName;
            TeamName = teamName;
            Origin = new CrawledAreaMemberOrigin.Team(orgName, teamName);
        }

        public string QualifiedName { get; }
        public string OrgName { get; }
        public string TeamName { get; }
        public CrawledAreaMemberOrigin Origin { get; }
        public IReadOnlyList<string> Members { get; set; } = [];
    }
}
