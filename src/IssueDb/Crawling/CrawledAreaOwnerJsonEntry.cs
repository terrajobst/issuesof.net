namespace IssueDb.Crawling;

public sealed class CrawledAreaOwnerJsonEntry
{
    public string Label { get; }
    public string Lead { get; }
    public string Pod { get; }
    public string[] Owners { get; }

    public CrawledAreaOwnerJsonEntry(string label, string lead, string pod, string[] owners)
    {
        Label = label;
        Lead = lead;
        Pod = pod;
        Owners = owners;
    }
}
