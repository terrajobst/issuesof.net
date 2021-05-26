using System.Collections.Generic;
using System.Linq;

namespace IssueDb
{
    public sealed class CrawledAreaOwnerEntry
    {
        public CrawledAreaOwnerEntry(string area, string lead, IReadOnlyList<string> owners)
        {
            Area = area;
            Lead = lead;
            Owners = owners;
        }

        public string Area { get; }
        public string Lead { get; }
        public IReadOnlyList<string> Owners { get; }
    }
}
