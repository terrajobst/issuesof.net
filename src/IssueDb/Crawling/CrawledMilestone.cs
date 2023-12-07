namespace IssueDb.Crawling;

public sealed class CrawledMilestone
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Number { get; set; }
}
