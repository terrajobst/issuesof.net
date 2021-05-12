using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IssuesOfDotNet.Querying
{
    public sealed class IssueQuery
    {
        public IssueQuery(IEnumerable<IssueFilter> filters)
        {
            Filters = filters.ToArray();
        }

        public IssueFilter[] Filters { get; set; }

        public static IssueQuery Create(string query)
        {
            var syntax = QuerySyntax.Parse(query);
            var expression = BoundQuery.Create(syntax);
            return Create(expression);
        }

        private static IssueQuery Create(BoundQuery node)
        {
            var filters = new List<IssueFilter>();

            foreach (var or in FlattenOrs(node))
            {
                var filter = CreateFilter(or);
                filters.Add(filter);
            }

            return new IssueQuery(filters);
        }

        private static IssueFilter CreateFilter(BoundQuery node)
        {
            var result = new IssueFilter();

            foreach (var and in FlattenAnds(node))
            {
                switch (and)
                {
                    case BoundKevValueQuery kevValueExpression:
                        Apply(result, kevValueExpression);
                        break;
                    case BoundTextQuery textExpression:
                        Apply(result, textExpression);
                        break;
                    default:
                        throw new Exception($"Unexpected node {and.GetType()}");
                }
            }

            return result;
        }

        private static void Apply(IssueFilter result, BoundKevValueQuery expression)
        {
            var key = expression.Key.ToLowerInvariant();
            var value = expression.Value.ToLowerInvariant();

            switch ((key, value))
            {
                case ("is", "open"):
                case ("state", "open"):
                    result.IsOpen = !expression.IsNegated;
                    break;
                case ("is", "closed"):
                case ("state", "closed"):
                    result.IsOpen = expression.IsNegated;
                    break;
                case ("is", "locked"):
                    result.IsLocked = !expression.IsNegated;
                    break;
                case ("is", "unlocked"):
                    result.IsLocked = expression.IsNegated;
                    break;
                case ("is", "pr"):
                case ("type", "pr"):
                    result.IsPullRequest = !expression.IsNegated;
                    break;
                case ("is", "issue"):
                case ("type", "issue"):
                    result.IsPullRequest = expression.IsNegated;
                    break;
                case ("is", "merged"):
                case ("state", "merged"):
                    result.IsMerged = !expression.IsNegated;
                    break;
                case ("is", "unmerged"):
                case ("state", "unmerged"):
                    result.IsMerged = expression.IsNegated;
                    break;

                case ("is", "draft"):
                case ("draft", "true"):
                    result.IsDraft = !expression.IsNegated;
                    break;
                case ("draft", "false"):
                    result.IsDraft = expression.IsNegated;
                    break;

                case ("archived", "true"):
                    result.IsArchived = !expression.IsNegated;
                    break;
                case ("archived", "false"):
                    result.IsArchived = expression.IsNegated;
                    break;

                case ("no", "assignee"):
                    result.NoAssignees = !expression.IsNegated;
                    break;
                case ("no", "label"):
                    result.NoLabels = !expression.IsNegated;
                    break;
                case ("no", "area"):
                    result.NoArea = !expression.IsNegated;
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

                case ("area", _):
                    if (expression.IsNegated)
                        result.ExcludedLabels.Add($"area-{value}");
                    else
                        result.IncludedLabels.Add($"area-{value}");
                    break;
                case ("area-under", _):
                    if (expression.IsNegated)
                        result.ExcludedAreas.Add(value);
                    else
                        result.IncludedAreas.Add(value);
                    break;

                case ("sort", "created"):
                case ("sort", "created-asc"):
                    result.Sort.Add(IssueSort.CreatedAscending);
                    break;
                case ("sort", "created-desc"):
                    result.Sort.Add(IssueSort.CreatedDescending);
                    break;
                case ("sort", "updated"):
                case ("sort", "updated-asc"):
                    result.Sort.Add(IssueSort.UpdatedAscending);
                    break;
                case ("sort", "updated-desc"):
                    result.Sort.Add(IssueSort.UpdatedDescending);
                    break;
                case ("sort", "comments"):
                case ("sort", "comments-asc"):
                    result.Sort.Add(IssueSort.CommentsAscending);
                    break;
                case ("sort", "comments-desc"):
                    result.Sort.Add(IssueSort.CommentsDescending);
                    break;
                case ("sort", "reactions"):
                case ("sort", "reactions-asc"):
                    result.Sort.Add(IssueSort.ReactionsAscending);
                    break;
                case ("sort", "reactions-desc"):
                    result.Sort.Add(IssueSort.ReactionsDescending);
                    break;
                case ("sort", "reactions-+1"):
                case ("sort", "reactions-+1-asc"):
                    result.Sort.Add(IssueSort.ReactionsPlus1Ascending);
                    break;
                case ("sort", "reactions-+1-desc"):
                    result.Sort.Add(IssueSort.ReactionsPlus1Descending);
                    break;
                case ("sort", "reactions--1"):
                case ("sort", "reactions--1-asc"):
                    result.Sort.Add(IssueSort.ReactionsMinus1Ascending);
                    break;
                case ("sort", "reactions--1-desc"):
                    result.Sort.Add(IssueSort.ReactionsMinus1Descending);
                    break;
                case ("sort", "reactions-smile"):
                case ("sort", "reactions-smile-asc"):
                    result.Sort.Add(IssueSort.ReactionsSmileAscending);
                    break;
                case ("sort", "reactions-smile-desc"):
                    result.Sort.Add(IssueSort.ReactionsSmileDescending);
                    break;
                case ("sort", "reactions-heart"):
                case ("sort", "reactions-heart-asc"):
                    result.Sort.Add(IssueSort.ReactionsHeartAscending);
                    break;
                case ("sort", "reactions-heart-desc"):
                    result.Sort.Add(IssueSort.ReactionsHeartDescending);
                    break;
                case ("sort", "reactions-tada"):
                case ("sort", "reactions-tada-asc"):
                    result.Sort.Add(IssueSort.ReactionsTadaAscending);
                    break;
                case ("sort", "reactions-tada-desc"):
                    result.Sort.Add(IssueSort.ReactionsTadaDescending);
                    break;
                case ("sort", "reactions-thinking_face"):
                case ("sort", "reactions-thinking_face-asc"):
                    result.Sort.Add(IssueSort.ReactionsThinkingFaceAscending);
                    break;
                case ("sort", "reactions-thinking_face-desc"):
                    result.Sort.Add(IssueSort.ReactionsThinkingFaceDescending);
                    break;
                case ("sort", "interactions"):
                case ("sort", "interactions-asc"):
                    result.Sort.Add(IssueSort.InteractionsAscending);
                    break;
                case ("sort", "interactions-desc"):
                    result.Sort.Add(IssueSort.InteractionsDescending);
                    break;

                default:
                    Apply(result, new BoundTextQuery(expression.IsNegated, $"{key}:{value}"));
                    break;
            }
        }

        private static void Apply(IssueFilter result, BoundTextQuery expression)
        {
            var terms = TextTokenizer.Tokenize(expression.Text);
            foreach (var term in terms)
            {
                if (expression.IsNegated)
                    result.ExcludedTerms.Add(term);
                else
                    result.IncludedTerms.Add(term);
            }
        }

        private static IEnumerable<BoundQuery> FlattenAnds(BoundQuery node)
        {
            var stack = new Stack<BoundQuery>();
            var result = new List<BoundQuery>();
            stack.Push(node);

            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n is not BoundAndQuery and)
                {
                    result.Add(n);
                }
                else
                {
                    stack.Push(and.Right);
                    stack.Push(and.Left);
                }
            }

            return result;
        }

        private static IEnumerable<BoundQuery> FlattenOrs(BoundQuery node)
        {
            var stack = new Stack<BoundQuery>();
            var result = new List<BoundQuery>();
            stack.Push(node);

            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n is not BoundOrQuery or)
                {
                    result.Add(n);
                }
                else
                {
                    stack.Push(or.Right);
                    stack.Push(or.Left);
                }
            }

            return result;
        }

        public void WriteTo(TextWriter writer)
        {
            if (writer is IndentedTextWriter indentedTextWriter)
            {
                WriteTo(indentedTextWriter);
            }
            else
            {
                indentedTextWriter = new IndentedTextWriter(writer);
                WriteTo(indentedTextWriter);
            }
        }

        private void WriteTo(IndentedTextWriter writer)
        {
            if (!Filters.Any())
                return;

            if (Filters.Length > 1)
            {
                writer.WriteLine("OR");
                writer.Indent++;
            }

            foreach (var filter in Filters)
                filter.WriteTo(writer);

            if (Filters.Length > 1)
            {
                writer.Indent--;
            }
        }

        public override string ToString()
        {
            using var writer = new StringWriter();
            WriteTo(writer);
            return writer.ToString();
        }
    }
}
