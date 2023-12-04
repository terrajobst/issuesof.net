namespace IssueDb;

#warning Rename them with the Crawled prefix

// TODO: Consider splitting owners into owners and subscribers
//       We could decide that ownership defined via teams only
//       counts as subscribers when merging

public sealed class AreaEntry
{
    public AreaEntry(string area,
                     IReadOnlyList<AreaMember> leads,
                     IReadOnlyList<AreaMember> owners)
    {
        Area = area;
        Leads = leads;
        Owners = owners;
    }

    public string Area { get; }
    public IReadOnlyList<AreaMember> Leads { get; }
    public IReadOnlyList<AreaMember> Owners { get; }
}
