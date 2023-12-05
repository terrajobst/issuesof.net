﻿namespace IssueDb;

public sealed class CrawledAreaMember
{
    public CrawledAreaMember(CrawledAreaMemberOrigin origin,
                             string userName)
    {
        Origin = origin;
        UserName = userName;
        IsPrimary = ComputeIsPrimary(origin);
    }

    public CrawledAreaMemberOrigin Origin { get; }
    public string UserName { get; }
    public bool IsPrimary { get; }

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
}
