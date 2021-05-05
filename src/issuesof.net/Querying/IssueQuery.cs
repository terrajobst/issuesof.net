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
                    Apply(result, new BoundTextQuery(expression.IsNegated, $"{key}:{value}"));
                    break;
            }
        }

        private static void Apply(IssueFilter result, BoundTextQuery expression)
        {
            if (expression.IsNegated)
                result.ExcludedTerms.Add(expression.Text);
            else
                result.IncludedTerms.Add(expression.Text);
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
                    stack.Push(and.Left);
                    stack.Push(and.Right);
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
                    stack.Push(or.Left);
                    stack.Push(or.Right);
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
