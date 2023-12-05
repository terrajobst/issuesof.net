namespace IssueDb;

public sealed class CrawledAreaMember
{
    public CrawledAreaMember(CrawledAreaMemberOrigin origin,
                             string userName)
    {
        Origin = origin;
        UserName = userName;
    }

    public CrawledAreaMemberOrigin Origin { get; }
    public string UserName { get; }

    public bool IsPrimary()
    {
        // We consider the origin to be primary if it wasn't expanded
        // by a team.

        if (Origin is CrawledAreaMemberOrigin.Composite c)
        {
            // A composite can also occur if multiple repos define a given member.
            // in their area-owners.md file.
            //
            // In this case we'd still consider it primary.
            return !c.Origins.Any(c => c is CrawledAreaMemberOrigin.Team);
        }
        else
        {
            return true;
        }
    }
}
