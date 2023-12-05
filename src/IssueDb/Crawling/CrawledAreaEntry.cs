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
        Definitions = GetDefinitions();
    }

    public string Area { get; }
    public IReadOnlyList<CrawledAreaMember> Leads { get; }
    public IReadOnlyList<CrawledAreaMember> Owners { get; }
    public IReadOnlyList<CrawledAreaMemberOrigin.File> Definitions { get; }

    private IReadOnlyList<CrawledAreaMemberOrigin.File> GetDefinitions()
    {        
        var seenDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CrawledAreaMemberOrigin.File>();

        foreach (var member in Leads.Concat(Owners))
        {
            Walk(member.Origin, seenDefinitions, result);
        }

        return result.ToArray();

        static void Walk(CrawledAreaMemberOrigin origin, HashSet<string> seenDefinitions, List<CrawledAreaMemberOrigin.File> result)
        {
            switch (origin)
            {
                case CrawledAreaMemberOrigin.Composite c:
                    foreach (var o in c.Origins)
                        Walk(o, seenDefinitions, result);
                    break;

                case CrawledAreaMemberOrigin.File f:
                    if (seenDefinitions.Add(f.ToShortString()))
                        result.Add(f);
                    break;
            }
        }
    }
}
