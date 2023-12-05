namespace IssueDb;

// TODO: Consider splitting owners into owners and subscribers
//       We could decide that ownership defined via teams only
//       counts as subscribers when merging

public sealed class CrawledAreaEntry
{
    public CrawledAreaEntry(string area,
                            IReadOnlyList<CrawledAreaMember> leads,
                            IReadOnlyList<CrawledAreaMember> owners)
    {
        Area = area;
        Leads = leads;
        Owners = owners;
    }

    public string Area { get; }
    public IReadOnlyList<CrawledAreaMember> Leads { get; }
    public IReadOnlyList<CrawledAreaMember> Owners { get; }
}
