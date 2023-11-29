using System.Collections.Generic;
using System.Linq;

using IssueDb.Querying;

namespace IssueDb.Crawling;

public abstract partial class CrawledIssueResults
{
    private sealed class ArrayIssueResults : CrawledIssueResults
    {
        private readonly CrawledIssue[] _issues;
        private readonly IReadOnlyCollection<IssueSort> _sorts;

        public ArrayIssueResults(CrawledIssue[] issues, IReadOnlyCollection<IssueSort> sorts)
        {
            _issues = issues;
            _sorts = sorts;
        }

        public override IReadOnlyCollection<IssueSort> Sorts => _sorts;

        public override int ItemCount => _issues.Length;

        public override int IssueCount => _issues.Length;

        public override IEnumerable<CrawledIssueOrGroup> Roots => _issues.Select(i => (CrawledIssueOrGroup)i);

        public override IEnumerable<CrawledIssueOrGroup> GetPage(int pageNumber)
        {
            return _issues.Skip((pageNumber - 1) * ItemsPerPage)
                          .Take(ItemsPerPage)
                          .Select(i => (CrawledIssueOrGroup)i);
        }
    }
}
