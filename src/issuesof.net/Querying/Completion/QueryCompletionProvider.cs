using System;
using System.Collections.Generic;

namespace IssuesOfDotNet.Querying
{
    public abstract class QueryCompletionProvider
    {
        public QueryCompletionResult Complete(ExpressionSyntax node, int position)
        {
            if (node is TextExpressionSyntax text)
            {
                var completions = GetCompletionsForText(text.TextToken.Value);
                return new QueryCompletionResult(completions, text.TextToken.Span);
            }

            if (node is KeyValueExpressionSyntax keyValue)
            {
                if (position < keyValue.ColonToken.Span.End)
                    return null;

                var completions = GetCompletionForKeyValue(keyValue.KeyToken.Value, keyValue.ValueToken.Value);
                return new QueryCompletionResult(completions, keyValue.ValueToken.Span);
            }

            var children = node.GetChildren();

            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var nextChild = i < children.Length - 1
                                    ? children[i + 1]
                                    : null;

                var start = child.Span.Start;
                var end = nextChild == null ? int.MaxValue : nextChild.Span.Start;

                if (start <= position && position < end)
                {
                    if (child is ExpressionSyntax expression)
                        return Complete(expression, position);
                    else
                        return null;
                }
            }

            return null;
        }

        public virtual IEnumerable<string> GetCompletionForKeyValue(string key, string value) => Array.Empty<string>();
        
        public virtual IEnumerable<string> GetCompletionsForText(string text) => Array.Empty<string>();
    }
}
