using System.Diagnostics;
using System.Net;

using IssueDb;

namespace IssuesOfDotNet.Crawler;

internal static class AreaOwnershipLoader
{
    private const string AreaOwnersPath = "docs/area-owners.md";

    public static async Task<AreaOwnership> ExpandTeamsAsync(GitHubAppClient client, AreaOwnership ownership)
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

        var entries = new List<AreaEntry>();

        foreach (var entry in ownership.Entries)
        {
            var area = entry.Area;
            var expandedLeads = ExpandMembers(expandedTeams, entry.Leads);
            var expandedOwners = ExpandMembers(expandedTeams, entry.Owners);
            var expandedEntry = new AreaEntry(entry.Area, expandedLeads, expandedOwners);
            entries.Add(expandedEntry);
        }

        return new AreaOwnership(entries.ToArray());

        static void AddTeams(Dictionary<string, ParsedTeam> receiver, IEnumerable<AreaMember> members)
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

        static AreaMember[] ExpandMembers(Dictionary<string, ParsedTeam> expandedTeams, IEnumerable<AreaMember> members)
        {
            var expandedMembers = new List<AreaMember>();

            foreach (var member in members)
            {
                if (expandedTeams.TryGetValue(member.UserName, out var parsedTeam))
                {
                    foreach (var teamMember in parsedTeam.Members)
                    {
                        var expandedMember = new AreaMember(parsedTeam.Origin, teamMember);
                        expandedMembers.Add(expandedMember);
                    }
                }
            }

            return expandedMembers.ToArray();
        }
    }

    public static async Task<AreaOwnership> FromTeamsAsync(GitHubAppClient client, string orgName)
    {
        await client.UseInstallationTokenAsync(orgName);

        var teams = await client.InvokeAsync(c => c.Organization.Team.GetAll(orgName));
        var entries = new List<AreaEntry>();

        foreach (var team in teams)
        {
            if (!TextTokenizer.TryParseArea(team.Name, out var area))
                continue;

            var teamMembers = await client.InvokeAsync(c => c.Organization.Team.GetAllMembers(team.Id));

            var origin = AreaMemberOrigin.FromTeam(orgName, team.Name);
            var leads = Array.Empty<AreaMember>();
            var owners = teamMembers.Select(m => new AreaMember(origin, m.Login)).ToArray();
            var entry = new AreaEntry(area, leads, owners);
            entries.Add(entry);
        }

        return new AreaOwnership(entries.ToArray());
    }

    public static async Task<AreaOwnership> FromRepoAsync(string orgName, string repoName)
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

    private static AreaOwnership Parse(string orgName, string repoName, string path, string contents)
    {
        var lines = GetLines(contents);
        var entries = new List<AreaEntry>();

        foreach (var (line, lineIndex) in lines.Select((l, i) => (l, i)))
        {
            var parts = line.Split('|');
            if (parts.Length != 6)
                continue;

            var areaText = parts[1].Trim();
            var leadText = parts[2].Trim();
            var ownerText = parts[3].Trim();

            if (!TextTokenizer.TryParseArea(areaText, out var area))
                continue;

            var lineNumber = lineIndex + 1;
            var origin = AreaMemberOrigin.FromFile(orgName, repoName, path, lineNumber);

            var leads = GetUntaggedUserNames(leadText, origin);
            var owners = GetUntaggedUserNames(ownerText, origin);

            var entry = new AreaEntry(area, leads, owners);
            entries.Add(entry);
        }

        return new AreaOwnership(entries.ToArray());

        static AreaMember[] GetUntaggedUserNames(string text, AreaMemberOrigin origin)
        {
            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => GetUntaggedUserName(t.Trim()))
                       .Select(u => new AreaMember(origin, u))
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

    private sealed class ParsedTeam
    {
        public ParsedTeam(string qualifiedName, string orgName, string teamName)
        {
            QualifiedName = qualifiedName;
            OrgName = orgName;
            TeamName = teamName;
            Origin = AreaMemberOrigin.FromTeam(orgName, teamName);
        }

        public string QualifiedName { get; }
        public string OrgName { get; }
        public string TeamName { get; }
        public AreaMemberOrigin Origin { get; }
        public IReadOnlyList<string> Members { get; set; } = [];
    }
}
