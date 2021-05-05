using System;
using System.Collections.Generic;
using System.Linq;

namespace IssuesOfDotNet.Querying
{
    public sealed class CrawledIssueQuery
    {
        public CrawledIssueQuery(IEnumerable<CrawledIssueFilter> filters)
        {
            Filters = filters.ToArray();
        }

        public IReadOnlyList<CrawledIssueFilter> Filters { get; set; }

        public IEnumerable<CrawledIssue> Execute(CrawledIndex index)
        {
            var result = (HashSet<CrawledIssue>)null;

            foreach (var filter in Filters)
            {
                var next = Execute(index, filter);
                if (result is null)
                    result = next;
                else
                    result.UnionWith(next);
            }

            return (IEnumerable<CrawledIssue>)result ?? Array.Empty<CrawledIssue>();
        }

        private static HashSet<CrawledIssue> Execute(CrawledIndex index, CrawledIssueFilter filter)
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
                ApplyPredicate(ref result, index, i => !i.IsMerged);

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
                var issues = trie.LookupIssuesForTerm(term);
                if (result is null)
                    result = issues.ToHashSet();
                else
                    result.IntersectWith(issues);
            }

            static void ApplyNegatedTerm(ref HashSet<CrawledIssue> result, CrawledIndex index, string term)
            {
                if (result is null)
                    result = new HashSet<CrawledIssue>(index.Repos.SelectMany(r => r.Issues.Values));

                var issues = index.Trie.LookupIssuesForTerm(term);
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

        public static CrawledIssueQuery Create(string query)
        {
            var syntax = QuerySyntax.Parse(query);
            var expression = BoundExpression.Create(syntax);
            var text = expression.ToString();
            return Create(expression);
        }

        private static CrawledIssueQuery Create(BoundExpression node)
        {
            var filters = new List<CrawledIssueFilter>();

            foreach (var or in FlattenOrs(node))
            {
                var filter = CreateFilter(or);
                filters.Add(filter);
            }

            return new CrawledIssueQuery(filters);
        }

        private static CrawledIssueFilter CreateFilter(BoundExpression node)
        {
            var result = new CrawledIssueFilter();

            foreach (var and in FlattenAnds(node))
            {
                switch (and)
                {
                    case BoundKevValueExpression kevValueExpression:
                        Apply(result, kevValueExpression);
                        break;
                    case BoundTextExpression textExpression:
                        Apply(result, textExpression);
                        break;
                    default:
                        throw new Exception($"Unexpected node {and.GetType()}");
                }
            }

            return result;
        }

        private static void Apply(CrawledIssueFilter result, BoundKevValueExpression expression)
        {
            var key = expression.Key.ToLowerInvariant();
            var value = expression.Value.ToLowerInvariant();

            switch ((key, value))
            {
                case ("is", "open"):
                    result.IsOpen = !expression.IsNegated;
                    break;
                case ("is", "closed"):
                    result.IsOpen = expression.IsNegated;
                    break;
                case ("is", "pr"):
                    result.IsPullRequest = !expression.IsNegated;
                    break;
                case ("is", "issue"):
                    result.IsPullRequest = expression.IsNegated;
                    break;
                case ("is", "merged"):
                    result.IsMerged = !expression.IsNegated;
                    break;

                case ("no", "assignees"):
                    result.NoAssignees = !expression.IsNegated;
                    break;
                case ("no", "labels"):
                    result.NoLabels = !expression.IsNegated;
                    break;
                case ("no", "milestone"):
                    result.NoMilestone = !expression.IsNegated;
                    break;

                case ("org", _):
                    if (expression.IsNegated)
                        result.ExcludedOrgs.Add(value);
                    else
                        result.IncludedOrgs.Add(value);
                    break;

                case ("repo", _):
                    if (expression.IsNegated)
                        result.ExcludedRepos.Add(value);
                    else
                        result.IncludedRepos.Add(value);
                    break;

                case ("author", _):
                    if (expression.IsNegated)
                        result.ExcludedAuthors.Add(value);
                    else
                        result.Author = value;
                    break;

                case ("assignee", _):
                    if (expression.IsNegated)
                        result.ExcludedAssignees.Add(value);
                    else
                        result.IncludedAssignees.Add(value);
                    break;

                case ("label", _):
                    if (expression.IsNegated)
                        result.ExcludedLabels.Add(value);
                    else
                        result.IncludedLabels.Add(value);
                    break;

                case ("milestone", _):
                    if (expression.IsNegated)
                        result.ExcludedMilestones.Add(value);
                    else
                        result.Milestone = value;
                    break;

                default:
                    Apply(result, new BoundTextExpression(expression.IsNegated, $"{key}:{value}"));
                    break;
            }
        }

        private static void Apply(CrawledIssueFilter result, BoundTextExpression expression)
        {
            if (expression.IsNegated)
                result.ExcludedTerms.Add(expression.Text);
            else
                result.IncludedTerms.Add(expression.Text);
        }

        private static IEnumerable<BoundExpression> FlattenAnds(BoundExpression node)
        {
            var stack = new Stack<BoundExpression>();
            var result = new List<BoundExpression>();
            stack.Push(node);

            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n is not BoundAndExpression and)
                {
                    result.Add(n);
                }
                else
                {
                    stack.Push(and.Left);
                    stack.Push(and.Right);
                }
            }

            return result;
        }

        private static IEnumerable<BoundExpression> FlattenOrs(BoundExpression node)
        {
            var stack = new Stack<BoundExpression>();
            var result = new List<BoundExpression>();
            stack.Push(node);

            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n is not BoundOrExpression or)
                {
                    result.Add(n);
                }
                else
                {
                    stack.Push(or.Left);
                    stack.Push(or.Right);
                }
            }

            return result;
        }
    }
}
