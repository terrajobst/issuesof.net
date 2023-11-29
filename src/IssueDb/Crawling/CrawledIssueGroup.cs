namespace IssueDb.Crawling;

public sealed class CrawledIssueGroup
{
    public CrawledIssueGroup(string[] keys, CrawledIssueOrGroup[] children)
    {
        Keys = keys;
        Children = children;
    }

    public string[] Keys { get; }
    public CrawledIssueOrGroup[] Children { get; }
}
