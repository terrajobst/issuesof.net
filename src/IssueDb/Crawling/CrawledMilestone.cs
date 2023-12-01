namespace IssueDb.Crawling;

public sealed class CrawledMilestone
{
    public long Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public long Number { get; set; }
}
