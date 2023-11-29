namespace IssueDb.Crawling;

public sealed class CrawledAreaOwnerJsonFile
{
    public CrawledAreaOwnerJsonEntry[] Areas { get; }
    public CrawledAreaOwnerJsonEntry[] OperatingSystems { get; }
    public CrawledAreaOwnerJsonEntry[] Architectures { get; }

    public CrawledAreaOwnerJsonFile(CrawledAreaOwnerJsonEntry[] areas, CrawledAreaOwnerJsonEntry[] operatingSystems, CrawledAreaOwnerJsonEntry[] architectures)
    {
        Areas = areas;
        OperatingSystems = operatingSystems;
        Architectures = architectures;
    }
}
