namespace IssueDb;

public sealed class AreaMember
{
    public AreaMember(AreaMemberOrigin origin,
                      string userName)
    {
        Origin = origin;
        UserName = userName;
    }

    public AreaMemberOrigin Origin { get; }
    public string UserName { get; }
}
