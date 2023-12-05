namespace IssueDb;

public abstract class CrawledAreaMemberOrigin
{
    public sealed class Composite(IReadOnlyList<CrawledAreaMemberOrigin> origins) : CrawledAreaMemberOrigin
    {
        public IReadOnlyList<CrawledAreaMemberOrigin> Origins { get; } = origins;
    }

    public sealed class File(string orgName, string repoName, string path, int lineNumber) : CrawledAreaMemberOrigin
    {
        public string OrgName { get; } = orgName;
        public string RepoName { get; } = repoName;
        public string Path { get; } = path;
        public int LineNumber { get; } = lineNumber;

        public string ToShortString()
        {
            return $"{OrgName}/{RepoName}/{Path}#{LineNumber}";
        }

        public string ToUrl()
        {
            return $"https://github.com/{OrgName}/{RepoName}/blob/main/{Path}?plain=1#L{LineNumber}";
        }
    }

    public sealed class Team(string orgName, string teamName) : CrawledAreaMemberOrigin
    {
        public string OrgName { get; } = orgName;
        public string TeamName { get; } = teamName;
    }

    public CrawledAreaMemberOrigin Merge(CrawledAreaMemberOrigin other)
    {
        var thisComposite = this as CrawledAreaMemberOrigin.Composite;
        var otherComposite = other as CrawledAreaMemberOrigin.Composite;

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
