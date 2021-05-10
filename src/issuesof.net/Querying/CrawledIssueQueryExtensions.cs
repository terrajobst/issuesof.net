using System;
using System.Collections.Generic;
using System.Linq;

namespace IssuesOfDotNet.Querying
{
    public static class CrawledIssueQueryExtensions
    {
        private static readonly IEnumerable<IssueSort> _defaultSort = new[] { IssueSort.UpdatedDescending };

        public static IEnumerable<CrawledIssue> Execute(this IssueQuery query, CrawledIndex index)
        {
            var result = (HashSet<CrawledIssue>)null;

            foreach (var filter in query.Filters)
            {
                var next = Execute(index, filter);
                if (result is null)
                    result = next;
                else
                    result.UnionWith(next);
            }

            if (result is null)
                return Array.Empty<CrawledIssue>();

            var sorts = query.Filters.SelectMany(f => f.Sort);
            if (!sorts.Any())
                sorts = _defaultSort;

            return Sort(result, sorts);
        }

        private static HashSet<CrawledIssue> Execute(CrawledIndex index, IssueFilter filter)
        {
            var result = (HashSet<CrawledIssue>)null;

            foreach (var term in filter.IncludedTerms)
                ApplyTerm(ref result, index.Trie, term);

            foreach (var org in filter.IncludedOrgs)
                ApplyTerm(ref result, index.Trie, $"org:{org}");

            foreach (var repo in filter.IncludedRepos)
                ApplyTerm(ref result, index.Trie, $"repo:{repo}");

            foreach (var assignee in filter.IncludedAssignees)
                ApplyTerm(ref result, index.Trie, $"assignee:{assignee}");

            foreach (var label in filter.IncludedLabels)
                ApplyTerm(ref result, index.Trie, $"label:{label}");

            if (filter.Author != null)
                ApplyTerm(ref result, index.Trie, $"author:{filter.Author}");

            if (filter.Milestone != null)
                ApplyTerm(ref result, index.Trie, $"milestone:{filter.Milestone}");

            if (filter.IsOpen == true)
                ApplyPredicate(ref result, index, i => i.State == CrawledIssueState.Open);
            else if (filter.IsOpen == false)
                ApplyPredicate(ref result, index, i => i.State == CrawledIssueState.Closed);

            if (filter.IsPullRequest == true)
                ApplyPredicate(ref result, index, i => i.IsPullRequest);
            else if (filter.IsPullRequest == false)
                ApplyPredicate(ref result, index, i => !i.IsPullRequest);

            if (filter.IsMerged == true)
                ApplyPredicate(ref result, index, i => i.IsMerged);
            else if (filter.IsMerged == false)
                ApplyPredicate(ref result, index, i => i.IsPullRequest && !i.IsMerged);

            if (filter.IsDraft == true)
                ApplyPredicate(ref result, index, i => i.IsDraft);
            else if (filter.IsDraft == false)
                ApplyPredicate(ref result, index, i => i.IsPullRequest && !i.IsDraft);

            if (filter.NoAssignees == true)
                ApplyPredicate(ref result, index, i => i.Assignees.Length == 0);
            else if (filter.NoAssignees == false)
                ApplyPredicate(ref result, index, i => i.Assignees.Length > 0);

            if (filter.NoLabels == true)
                ApplyPredicate(ref result, index, i => i.Labels.Length == 0);
            else if (filter.NoLabels == false)
                ApplyPredicate(ref result, index, i => i.Labels.Length > 0);

            if (filter.NoMilestone == true)
                ApplyPredicate(ref result, index, i => i.Milestone is null);
            else if (filter.NoMilestone == false)
                ApplyPredicate(ref result, index, i => i.Milestone is not null);

            foreach (var org in filter.ExcludedOrgs)
                ApplyNegatedTerm(ref result, index, $"org:{org}");

            foreach (var repo in filter.ExcludedRepos)
                ApplyNegatedTerm(ref result, index, $"repo:{repo}");

            foreach (var term in filter.ExcludedTerms)
                ApplyNegatedTerm(ref result, index, term);

            foreach (var assignee in filter.ExcludedAssignees)
                ApplyNegatedTerm(ref result, index, $"assignee:{assignee}");

            foreach (var label in filter.ExcludedLabels)
                ApplyNegatedTerm(ref result, index, $"label:{label}");

            foreach (var milestone in filter.ExcludedMilestones)
                ApplyNegatedTerm(ref result, index, $"milestone:{milestone}");

            return result ?? new HashSet<CrawledIssue>();

            static void ApplyTerm(ref HashSet<CrawledIssue> result, CrawledTrie trie, string term)
            {
                var issues = trie.Lookup(term);
                if (result is null)
                    result = issues.ToHashSet();
                else
                    result.IntersectWith(issues);
            }

            static void ApplyNegatedTerm(ref HashSet<CrawledIssue> result, CrawledIndex index, string term)
            {
                if (result is null)
                    result = new HashSet<CrawledIssue>(index.Repos.SelectMany(r => r.Issues.Values));

                var issues = index.Trie.Lookup(term);
                result.ExceptWith(issues);
            }

            static void ApplyPredicate(ref HashSet<CrawledIssue> result, CrawledIndex index, Func<CrawledIssue, bool> predicate)
            {
                if (result is null)
                    result = new HashSet<CrawledIssue>(index.Repos.SelectMany(r => r.Issues.Values).Where(predicate));
                else
                    result.RemoveWhere(i => !predicate(i));
            }
        }

        private static IEnumerable<CrawledIssue> Sort(IEnumerable<CrawledIssue> result, IEnumerable<IssueSort> sorts)
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
                }
            }

            return result;
        }
    }
}
