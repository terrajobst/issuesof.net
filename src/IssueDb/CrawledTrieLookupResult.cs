using System;
using System.Collections.Generic;
using System.Linq;

namespace IssuesOfDotNet
{
    public sealed class CrawledTrieLookupResult
    {
        public static CrawledTrieLookupResult Empty => new(Array.Empty<CrawledIssue>());

        private const int ItemsPerPage = 25;
        private readonly IReadOnlyCollection<CrawledIssue> _issues;

        public CrawledTrieLookupResult(IEnumerable<CrawledIssue> issues)
        {
            _issues = issues.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt).ToArray();
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
