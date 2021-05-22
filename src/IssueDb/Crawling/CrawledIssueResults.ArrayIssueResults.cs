using System.Collections.Generic;
using System.Linq;

namespace IssueDb.Crawling
{
    public abstract partial class CrawledIssueResults
    {
        private sealed class ArrayIssueResults : CrawledIssueResults
        {
            private readonly CrawledIssue[] _issues;

            public ArrayIssueResults(CrawledIssue[] issues)
            {
                _issues = issues;
            }

            public override int ItemCount => _issues.Length;

            public override int IssueCount => _issues.Length;

            public override IEnumerable<CrawledIssueOrGroup> GetPage(int pageNumber)
            {
                return _issues.Skip((pageNumber - 1) * ItemsPerPage)
                              .Take(ItemsPerPage)
                              .Select(i => (CrawledIssueOrGroup)i);
            }
        }
    }
}
