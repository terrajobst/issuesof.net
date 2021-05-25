using System.Collections.Generic;
using System.Linq;

namespace IssueDb
{
    public sealed class CrawledAreaOwnerEntry
    {
        public CrawledAreaOwnerEntry(string area, string lead, IEnumerable<string> owners)
        {
            Area = area;
            Lead = lead;
            Owners = owners.ToArray();
        }

        public string Area { get; }
        public string Lead { get; }
        public IReadOnlyList<string> Owners { get; }
    }
}
