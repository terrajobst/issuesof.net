using System;
using System.Collections.Generic;
using System.Linq;

namespace IssueDb.Crawling
{
    public readonly struct CrawledIssueResults
    {
        public static CrawledIssueResults Empty => new(Array.Empty<CrawledIssue>());

        private const int ItemsPerPage = 25;
        private readonly IReadOnlyCollection<CrawledIssue> _issues;

        public CrawledIssueResults(IEnumerable<CrawledIssue> issues)
        {
            _issues = issues.ToArray();
        }

        public bool IsEmpty => _issues.Count == 0;

        public int PageCount => (int)Math.Ceiling(_issues.Count / (float)ItemsPerPage);

        public int TotalCount => _issues.Count;

        public IEnumerable<CrawledIssue> GetPage(int pageNumber)
        {
            return _issues.Skip((pageNumber - 1) * ItemsPerPage)
                          .Take(ItemsPerPage);
        }
    }
}
