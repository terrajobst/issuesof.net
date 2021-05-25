using System;
using System.Collections.Generic;
using System.Linq;

using IssueDb.Querying;

namespace IssueDb.Crawling
{
    public sealed class CrawledIssueGroupKey
    {
        private const string None = "(None)";
        private readonly Func<CrawledIssue, string> _singleGrouper;
        private readonly Func<CrawledIssue, IEnumerable<string>> _multiGrouper;

        public CrawledIssueGroupKey(Func<CrawledIssue, string> grouper)
        {
            _singleGrouper = grouper;
        }

        public CrawledIssueGroupKey(Func<CrawledIssue, IEnumerable<string>> grouper)
        {
            _multiGrouper = grouper;
        }

        public IEnumerable<IGrouping<string, CrawledIssue>> Group(IEnumerable<CrawledIssue> issues)
        {
            if (_singleGrouper is not null)
            {
                return issues.GroupBy(i => _singleGrouper(i) ?? None)
                             .OrderBy(g => g.Key);
            }
            else
            {
                return issues.SelectMany(i => _multiGrouper(i).DefaultIfEmpty(), (issue, key) => (Issue: issue, Key: key ?? None))
                             .GroupBy(t => t.Key, t => t.Issue)
                             .OrderBy(g => g.Key);
            }
        }

        public static CrawledIssueGroupKey Get(IssueGroup group)
        {
            return group switch
            {
                IssueGroup.Org => Org,
                IssueGroup.Repo => Repo,
                IssueGroup.Creator => Creator,
                IssueGroup.Assignee => Assignee,
                IssueGroup.Label => Label,
                IssueGroup.Milestone => Milestone,
                IssueGroup.Area => Area,
                IssueGroup.AreaNode => AreaNode,
                IssueGroup.AreaUnder => AreaUnder,
                IssueGroup.AreaLead => AreaLead,
                IssueGroup.AreaOwner => AreaOwner,
                _ => throw new Exception($"Unexpected group {group}"),
            };
        }

        public static CrawledIssueGroupKey Org => new(i => i.Repo.Org);

        public static CrawledIssueGroupKey Repo => new(i => i.Repo.FullName);

        public static CrawledIssueGroupKey Creator => new(i => i.CreatedBy);

        public static CrawledIssueGroupKey Assignee => new(i => i.Assignees);

        public static CrawledIssueGroupKey Label => new(i => i.Labels.Select(l => l.Name));

        public static CrawledIssueGroupKey Milestone => new(i => i.Milestone?.Title);

        public static CrawledIssueGroupKey Area => new(i => i.DirectAreaNodes.Distinct(StringComparer.OrdinalIgnoreCase));

        public static CrawledIssueGroupKey AreaNode => new(i => i.AreaNodes.Distinct(StringComparer.OrdinalIgnoreCase));

        public static CrawledIssueGroupKey AreaUnder => new(i => i.Areas.Distinct(StringComparer.OrdinalIgnoreCase));

        public static CrawledIssueGroupKey AreaLead => new(i => i.AreaLeads);

        public static CrawledIssueGroupKey AreaOwner => new(i => i.AreaOwners);

    }
}
