using System;
using System.Collections.Generic;
using System.Linq;

using IssueDb.Querying;

namespace IssueDb.Crawling;

public sealed class CrawledIssueGroupKey
{
    private const string None = "(None)";
    private readonly Func<CrawledIssue, string> _singleGrouper;
    private readonly Func<CrawledIssue, IEnumerable<string>> _multiGrouper;

    public CrawledIssueGroupKey(IssueGroup group, Func<CrawledIssue, string> grouper)
    {
        Group = group;
        _singleGrouper = grouper;
    }

    public CrawledIssueGroupKey(IssueGroup group, Func<CrawledIssue, IEnumerable<string>> grouper)
    {
        Group = group;
        _multiGrouper = grouper;
    }

    public IssueGroup Group { get; }

    public IEnumerable<IGrouping<string, CrawledIssue>> Apply(IEnumerable<CrawledIssue> issues)
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
            IssueGroup.Author => Author,
            IssueGroup.Assignee => Assignee,
            IssueGroup.Label => Label,
            IssueGroup.Milestone => Milestone,
            IssueGroup.Area => Area,
            IssueGroup.AreaNode => AreaNode,
            IssueGroup.AreaUnder => AreaUnder,
            IssueGroup.AreaLead => AreaLead,
            IssueGroup.AreaPod => AreaPod,
            IssueGroup.AreaOwner => AreaOwner,
            _ => throw new Exception($"Unexpected group {group}"),
        };
    }

    public static CrawledIssueGroupKey Org => new(IssueGroup.Org, i => i.Repo.Org);

    public static CrawledIssueGroupKey Repo => new(IssueGroup.Repo, i => i.Repo.FullName);

    public static CrawledIssueGroupKey Author => new(IssueGroup.Author, i => i.CreatedBy);

    public static CrawledIssueGroupKey Assignee => new(IssueGroup.Assignee, i => i.Assignees);

    public static CrawledIssueGroupKey Label => new(IssueGroup.Label, i => i.Labels.Select(l => l.Name));

    public static CrawledIssueGroupKey Milestone => new(IssueGroup.Milestone, i => i.Milestone?.Title);

    public static CrawledIssueGroupKey Area => new(IssueGroup.Area, i => i.DirectAreaNodes.Distinct(StringComparer.OrdinalIgnoreCase));

    public static CrawledIssueGroupKey AreaNode => new(IssueGroup.AreaNode, i => i.AreaNodes.Distinct(StringComparer.OrdinalIgnoreCase));

    public static CrawledIssueGroupKey AreaUnder => new(IssueGroup.AreaUnder, i => i.Areas.Distinct(StringComparer.OrdinalIgnoreCase));

    public static CrawledIssueGroupKey AreaLead => new(IssueGroup.AreaLead, i => i.AreaLeads);

    public static CrawledIssueGroupKey AreaPod => new(IssueGroup.AreaPod, i => i.AreaPods);

    public static CrawledIssueGroupKey AreaOwner => new(IssueGroup.AreaOwner, i => i.AreaOwners);
}
