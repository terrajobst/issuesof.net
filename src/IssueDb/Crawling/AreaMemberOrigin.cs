namespace IssueDb;

public abstract class AreaMemberOrigin
{
    public static AreaMemberOrigin FromComposite(params AreaMemberOrigin[] origins)
    {
        return new Composite(origins);
    }

    public static AreaMemberOrigin FromFile(string orgName, string repoName, string path, int lineNumber)
    {
        return new File(orgName, repoName, path, lineNumber);
    }

    public static AreaMemberOrigin FromTeam(string orgName, string teamName)
    {
        return new Team(orgName, teamName);
    }

    public sealed class Composite(IReadOnlyList<AreaMemberOrigin> origins) : AreaMemberOrigin
    {
        public IReadOnlyList<AreaMemberOrigin> Origins { get; } = origins;
    }

    public sealed class File(string orgName, string repoName, string path, int lineNumber) : AreaMemberOrigin
    {
        public string OrgName { get; } = orgName;
        public string RepoName { get; } = repoName;
        public string Path { get; } = path;
        public int LineNumber { get; } = lineNumber;
    }

    public sealed class Team(string orgName, string teamName) : AreaMemberOrigin
    {
        public string OrgName { get; } = orgName;
        public string TeamName { get; } = teamName;
    }

    public AreaMemberOrigin Merge(AreaMemberOrigin other)
    {
        var thisComposite = this as AreaMemberOrigin.Composite;
        var otherComposite = other as AreaMemberOrigin.Composite;

        if (thisComposite is not null && otherComposite is not null)
        {
            return new Composite([.. thisComposite.Origins, .. otherComposite.Origins]);
        }
        else if (thisComposite is not null && otherComposite is null)
        {
            return new Composite([.. thisComposite.Origins, other]);
        }
        else if (thisComposite is null && otherComposite is not null)
        {
            return new Composite([this, .. otherComposite.Origins]);
        }
        else
        {
            return new Composite([this, other]);
        }
    }
}
