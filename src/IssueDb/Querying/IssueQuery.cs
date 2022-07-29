using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using IssueDb.Querying.Binding;
using IssueDb.Querying.Syntax;

namespace IssueDb.Querying
{
    public sealed partial class IssueQuery
    {
        public static IEnumerable<string> SupportedKeys => _keyValueHandlers.Keys.Select(kv => kv.Key).Distinct();

        public static IEnumerable<string> SupportedValues => _keyValueHandlers.Keys.Select(kv => kv.Value).Where(v => v is not null).Distinct();

        public static IEnumerable<string> SupportedValuesFor(string key) => _keyValueHandlers.Keys.Where(kv => kv.Value is not null &&
                                                                                                            string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                                                                                                  .Select(kv => kv.Value);

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
                    case BoundKeyValueQuery keyValueExpression:
                        Apply(result, keyValueExpression);
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

        private static void Apply(IssueFilter result, BoundKeyValueQuery expression)
        {
            var key = expression.Key.ToLowerInvariant();
            var value = expression.Value.ToLowerInvariant();

            if (_keyValueHandlers.TryGetValue((key, value), out var handler) ||
                _keyValueHandlers.TryGetValue((key, null), out handler))
            {
                handler(result, expression);
                return;
            }

            Apply(result, new BoundTextQuery(expression.IsNegated, $"{key}:{value}"));
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
