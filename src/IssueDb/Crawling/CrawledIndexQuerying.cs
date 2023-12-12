﻿using IssueDb.Querying;

namespace IssueDb.Crawling;

public static partial class CrawledIndexQuerying
{
    private static readonly IssueSort[] _defaultSort = new[] { IssueSort.UpdatedDescending };
    private static readonly IssueGroupSort[] _defaultGroupSort = new[] { IssueGroupSort.KeyAscending };

    public static CrawledIssueResults Execute(this IssueQuery query, CrawledIndex index)
    {
        var result = (HashSet<CrawledIssue>?)null;

        foreach (var filter in query.Filters)
        {
            var next = Execute(index, filter);
            if (result is null)
                result = next;
            else
                result.UnionWith(next);
        }

        if (result is null)
            return CrawledIssueResults.Empty;

        var sorts = query.Filters.SelectMany(f => f.Sort)
                                 .Distinct()
                                 .ToArray();

        if (!sorts.Any())
            sorts = _defaultSort;

        var groups = query.Filters.SelectMany(f => f.Groups)
                                  .Distinct()
                                  .Select(CrawledIssueGroupKey.Get)
                                  .ToArray();

        if (!groups.Any())
            return CrawledIssueResults.Create(result, sorts);

        var groupSorts = query.Filters.SelectMany(f => f.GroupSort)
                                      .Distinct()
                                      .ToArray();

        if (!groupSorts.Any())
            groupSorts = _defaultGroupSort;

        return CrawledIssueResults.Create(result, sorts, groups, groupSorts);
    }

    private static HashSet<CrawledIssue> Execute(CrawledIndex index, IssueFilter filter)
    {
        var result = (HashSet<CrawledIssue>?)null;

        foreach (var term in filter.IncludedTerms)
            ApplyTerm(ref result, index.Trie, term);

        if (filter.IsLocked == true)
            ApplyPredicate(ref result, index, i => i.IsLocked);
        else if (filter.IsLocked == false)
            ApplyPredicate(ref result, index, i => !i.IsLocked);

        if (filter.IsPullRequest == true)
            ApplyPredicate(ref result, index, i => i.IsPullRequest);
        else if (filter.IsPullRequest == false)
            ApplyPredicate(ref result, index, i => !i.IsPullRequest);

        if (filter.IsArchived == true)
            ApplyPredicate(ref result, index, i => i.Repo.IsArchived);
        else if (filter.IsArchived == false)
            ApplyPredicate(ref result, index, i => !i.Repo.IsArchived);

        if (filter.NoAssignees == true)
            ApplyPredicate(ref result, index, i => i.Assignees.Length == 0);
        else if (filter.NoAssignees == false)
            ApplyPredicate(ref result, index, i => i.Assignees.Length > 0);

        if (filter.NoLabels == true)
            ApplyPredicate(ref result, index, i => i.Labels.Length == 0);
        else if (filter.NoLabels == false)
            ApplyPredicate(ref result, index, i => i.Labels.Length > 0);

        if (filter.NoArea == true)
            ApplyPredicate(ref result, index, i => !i.Areas.Any());
        else if (filter.NoArea == false)
            ApplyPredicate(ref result, index, i => i.Areas.Any());

        if (filter.NoAreaLead == true)
            ApplyPredicate(ref result, index, i => !i.AreaLeads.Any());
        else if (filter.NoAreaLead == false)
            ApplyPredicate(ref result, index, i => i.AreaLeads.Any());

        if (filter.NoAreaOwner == true)
            ApplyPredicate(ref result, index, i => !i.AreaOwners.Any());
        else if (filter.NoAreaOwner == false)
            ApplyPredicate(ref result, index, i => i.AreaOwners.Any());

        if (filter.NoOperatingSystem == true)
            ApplyPredicate(ref result, index, i => !i.OperatingSystems.Any());
        else if (filter.NoOperatingSystem == false)
            ApplyPredicate(ref result, index, i => i.OperatingSystems.Any());

        if (filter.NoOperatingSystemLead == true)
            ApplyPredicate(ref result, index, i => !i.OperatingSystemLeads.Any());
        else if (filter.NoOperatingSystemLead == false)
            ApplyPredicate(ref result, index, i => i.OperatingSystemLeads.Any());

        if (filter.NoOperatingSystemOwner == true)
            ApplyPredicate(ref result, index, i => !i.OperatingSystemOwners.Any());
        else if (filter.NoOperatingSystemOwner == false)
            ApplyPredicate(ref result, index, i => i.OperatingSystemOwners.Any());

        if (filter.NoArchitecture == true)
            ApplyPredicate(ref result, index, i => !i.Architectures.Any());
        else if (filter.NoArchitecture == false)
            ApplyPredicate(ref result, index, i => i.Architectures.Any());

        if (filter.NoArchitectureLead == true)
            ApplyPredicate(ref result, index, i => !i.ArchitectureLeads.Any());
        else if (filter.NoArchitectureLead == false)
            ApplyPredicate(ref result, index, i => i.ArchitectureLeads.Any());

        if (filter.NoArchitectureOwner == true)
            ApplyPredicate(ref result, index, i => !i.ArchitectureOwners.Any());
        else if (filter.NoArchitectureOwner == false)
            ApplyPredicate(ref result, index, i => i.ArchitectureOwners.Any());

        if (filter.NoLead == true)
            ApplyPredicate(ref result, index, i => !i.Leads.Any());
        else if (filter.NoLead == false)
            ApplyPredicate(ref result, index, i => i.Leads.Any());

        if (filter.NoOwner == true)
            ApplyPredicate(ref result, index, i => !i.Owners.Any());
        else if (filter.NoOwner == false)
            ApplyPredicate(ref result, index, i => i.Owners.Any());

        if (filter.NoMilestone == true)
            ApplyPredicate(ref result, index, i => i.Milestone is null);
        else if (filter.NoMilestone == false)
            ApplyPredicate(ref result, index, i => i.Milestone is not null);

        if (filter.Created is not null)
            ApplyPredicate(ref result, index, i => filter.Created.Contains(i.CreatedAt));
        if (filter.Updated is not null)
            ApplyPredicate(ref result, index, i => i.UpdatedAt != null && filter.Updated.Contains(i.UpdatedAt.Value));
        if (filter.Closed is not null)
            ApplyPredicate(ref result, index, i => i.ClosedAt != null && filter.Closed.Contains(i.ClosedAt.Value));

        if (filter.Comments is not null)
            ApplyPredicate(ref result, index, i => filter.Comments.Contains(i.Comments));
        if (filter.Reactions is not null)
            ApplyPredicate(ref result, index, i => filter.Reactions.Contains(i.Reactions));
        if (filter.Interactions is not null)
            ApplyPredicate(ref result, index, i => filter.Interactions.Contains(i.Interactions));

        foreach (var term in filter.ExcludedTerms)
            ApplyNegatedTerm(ref result, index, term);

        return result ?? new HashSet<CrawledIssue>();

        static void ApplyTerm(ref HashSet<CrawledIssue>? result, CrawledTrie<CrawledIssue> trie, string term)
        {
            var issues = trie.Lookup(term);
            if (result is null)
                result = issues.ToHashSet();
            else
                result.IntersectWith(issues);
        }

        static void ApplyNegatedTerm(ref HashSet<CrawledIssue>? result, CrawledIndex index, string term)
        {
            if (result is null)
                result = new HashSet<CrawledIssue>(index.Repos.SelectMany(r => r.Issues.Values));

            var issues = index.Trie.Lookup(term);
            result.ExceptWith(issues);
        }

        static void ApplyPredicate(ref HashSet<CrawledIssue>? result, CrawledIndex index, Func<CrawledIssue, bool> predicate)
        {
            if (result is null)
                result = new HashSet<CrawledIssue>(index.Repos.SelectMany(r => r.Issues.Values).Where(predicate));
            else
                result.RemoveWhere(i => !predicate(i));
        }
    }

    public static IEnumerable<CrawledIssue> Sort(this IEnumerable<CrawledIssue> result, IEnumerable<IssueSort> sorts)
    {
        foreach (var sort in sorts)
        {
            var orderedEnumerable = result as IOrderedEnumerable<CrawledIssue>;

            switch (sort)
            {
                case IssueSort.CreatedAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.CreatedAt);
                    else
                        result = orderedEnumerable.ThenBy(i => i.CreatedAt);
                    break;
                case IssueSort.CreatedDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.CreatedAt);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.CreatedAt);
                    break;
                case IssueSort.UpdatedAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.UpdatedAt);
                    else
                        result = orderedEnumerable.ThenBy(i => i.UpdatedAt);
                    break;
                case IssueSort.UpdatedDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.UpdatedAt);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.UpdatedAt);
                    break;
                case IssueSort.CommentsAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.Comments);
                    else
                        result = orderedEnumerable.ThenBy(i => i.Comments);
                    break;
                case IssueSort.CommentsDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.Comments);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.Comments);
                    break;
                case IssueSort.ReactionsAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.Reactions);
                    else
                        result = orderedEnumerable.ThenBy(i => i.Reactions);
                    break;
                case IssueSort.ReactionsDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.Reactions);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.Reactions);
                    break;
                case IssueSort.ReactionsPlus1Ascending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.ReactionsPlus1);
                    else
                        result = orderedEnumerable.ThenBy(i => i.ReactionsPlus1);
                    break;
                case IssueSort.ReactionsPlus1Descending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.ReactionsPlus1);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.ReactionsPlus1);
                    break;
                case IssueSort.ReactionsMinus1Ascending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.ReactionsMinus1);
                    else
                        result = orderedEnumerable.ThenBy(i => i.ReactionsMinus1);
                    break;
                case IssueSort.ReactionsMinus1Descending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.ReactionsMinus1);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.ReactionsMinus1);
                    break;
                case IssueSort.ReactionsSmileAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.ReactionsSmile);
                    else
                        result = orderedEnumerable.ThenBy(i => i.ReactionsSmile);
                    break;
                case IssueSort.ReactionsSmileDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.ReactionsSmile);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.ReactionsSmile);
                    break;
                case IssueSort.ReactionsHeartAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.ReactionsHeart);
                    else
                        result = orderedEnumerable.ThenBy(i => i.ReactionsHeart);
                    break;
                case IssueSort.ReactionsHeartDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.ReactionsHeart);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.ReactionsHeart);
                    break;
                case IssueSort.ReactionsTadaAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.ReactionsTada);
                    else
                        result = orderedEnumerable.ThenBy(i => i.ReactionsTada);
                    break;
                case IssueSort.ReactionsTadaDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.ReactionsTada);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.ReactionsTada);
                    break;
                case IssueSort.ReactionsThinkingFaceAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.ReactionsThinkingFace);
                    else
                        result = orderedEnumerable.ThenBy(i => i.ReactionsThinkingFace);
                    break;
                case IssueSort.ReactionsThinkingFaceDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.ReactionsThinkingFace);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.ReactionsThinkingFace);
                    break;
                case IssueSort.InteractionsAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.Interactions);
                    else
                        result = orderedEnumerable.ThenBy(i => i.Interactions);
                    break;
                case IssueSort.InteractionsDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.Interactions);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.Interactions);
                    break;
            }
        }

        return result;
    }

    public static IEnumerable<CrawledIssueGroup> Sort(this IEnumerable<CrawledIssueGroup> result, IEnumerable<IssueGroupSort> sorts)
    {
        foreach (var sort in sorts)
        {
            var orderedEnumerable = result as IOrderedEnumerable<CrawledIssueGroup>;

            switch (sort)
            {
                case IssueGroupSort.KeyAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.Keys.Last());
                    else
                        result = orderedEnumerable.ThenBy(i => i.Keys.Last());
                    break;
                case IssueGroupSort.KeyDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.Keys.Last());
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.Keys.Last());
                    break;

                case IssueGroupSort.CountAscending:
                    if (orderedEnumerable is null)
                        result = result.OrderBy(i => i.Children.Length);
                    else
                        result = orderedEnumerable.ThenBy(i => i.Children.Length);
                    break;
                case IssueGroupSort.CountDescending:
                    if (orderedEnumerable is null)
                        result = result.OrderByDescending(i => i.Children.Length);
                    else
                        result = orderedEnumerable.ThenByDescending(i => i.Children.Length);
                    break;
            }
        }

        return result;
    }
}
