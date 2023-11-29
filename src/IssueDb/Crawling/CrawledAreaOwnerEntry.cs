using System.Collections.Generic;
using System.Linq;

namespace IssueDb;

public sealed class CrawledAreaOwnerEntry
{
    public CrawledAreaOwnerEntry(string area, string lead, string pod, IReadOnlyList<string> owners)
    {
        Area = area;
        Lead = lead;
        Pod = pod;
        Owners = owners;

        if (string.IsNullOrEmpty(pod) && owners is not null)
        {
            Pod = string.Join("-", owners.Where(o => !string.IsNullOrEmpty(o))
                                         .OrderBy(o => o)
                                         .Select(o => o.ToLower()));
        }
    }

    public string Area { get; }
    public string Lead { get; }
    public string Pod { get; }
    public IReadOnlyList<string> Owners { get; }
}
