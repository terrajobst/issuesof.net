namespace IssuesOfDotNet.Crawler;

internal sealed class GitHubTeamsManager
{
    private readonly GitHubAppClient _client;
    private readonly Dictionary<string, TeamData> _teamDataByOrgName = new(StringComparer.OrdinalIgnoreCase);

    public GitHubTeamsManager(GitHubAppClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<string>> ExpandTeamAsync(string orgName, string teamSlug)
    {
        if (!_teamDataByOrgName.TryGetValue(orgName, out var teamData))
        {
            teamData = new TeamData();

            var teams = await _client.InvokeAsync(c => c.Organization.Team.GetAll(orgName));
            foreach (var team in teams)
                teamData.TeamIdBySlug[team.Slug] = team.Id;

            _teamDataByOrgName.Add(orgName, teamData);
        }

        if (!teamData.TeamIdBySlug.TryGetValue(teamSlug, out var teamId))
        {
            return null;
        }

        if (!teamData.TeamMembersById.TryGetValue(teamId, out var result))
        {
            var members = await _client.InvokeAsync(c => c.Organization.Team.GetAllMembers(teamId));
            result = members.Select(u => u.Login).ToArray();
            teamData.TeamMembersById.Add(teamId, result);
        }

        return result;
    }

    private sealed class TeamData
    {
        public Dictionary<string, int> TeamIdBySlug = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, IReadOnlyList<string>> TeamMembersById = new();
    }
}
