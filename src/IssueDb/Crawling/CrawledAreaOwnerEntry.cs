namespace IssueDb;

#warning Remove me
public sealed class CrawledAreaOwnerEntry
{
    public CrawledAreaOwnerEntry(string area, IReadOnlyList<string> leads, IReadOnlyList<string> owners)
    {
        Area = area;
        Leads = leads;
        Owners = owners;
    }

    public string Area { get; }
    public IReadOnlyList<string> Leads { get; }
    public IReadOnlyList<string> Owners { get; }
}
