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
}
