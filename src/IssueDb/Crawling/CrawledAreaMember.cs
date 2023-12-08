namespace IssueDb;

public sealed class CrawledAreaMember
{
    public CrawledAreaMember(CrawledAreaMemberOrigin origin,
                             string userName)
    {
        Origin = origin;
        UserName = userName;
        IsPrimary = ComputeIsPrimary(origin);
        IsTeam = userName.Contains('/');
    }

    public CrawledAreaMemberOrigin Origin { get; }
    public string UserName { get; }
    public bool IsPrimary { get; }
    public bool IsTeam { get; }

    private static bool ComputeIsPrimary(CrawledAreaMemberOrigin origin)
    {
        // We consider the origin to be primary if it wasn't expanded
        // by a team.

        if (origin is CrawledAreaMemberOrigin.Composite c)
        {
            // A composite can occur in multiple cases:
            // - The area member is defined directly and indirectly via a team
            // - Multiple repos define a given member

            var remainder = c.Origins.ToList();

            for (var i = remainder.Count - 1; i >= 0; i--)
            {
                if (remainder[i] is CrawledAreaMemberOrigin.Team)
                    remainder.RemoveRange(i, 2);
            }

            foreach (var o in remainder)
            {
                if (o is CrawledAreaMemberOrigin.File)
                    return true;
            }

            return false;
        }
        else
        {
            return true;
        }
    }

    public string ToUrl()
    {
        var indexOfSlash = UserName.IndexOf('/');
        if (indexOfSlash < 0)
            return $"https://github.com/{UserName}";

        var orgName = UserName.Substring(0, indexOfSlash);
        var teamName =  UserName.Substring(indexOfSlash + 1);
        return $"https://github.com/orgs/{orgName}/teams/{teamName}";
    }
}
