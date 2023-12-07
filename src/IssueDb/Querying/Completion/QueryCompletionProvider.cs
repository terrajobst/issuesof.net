using System.Diagnostics;
using IssueDb.Querying.Syntax;

namespace IssueDb.Querying.Completion;

public abstract class QueryCompletionProvider
{
    public static QueryCompletionProvider Empty { get; } = new EmptyQueryCompletionProvider();

    public QueryCompletionResult? Complete(QuerySyntax node, int position)
    {
        if (node is TextQuerySyntax text)
            return GetTextCompletions(text);

        if (node is KeyValueQuerySyntax keyValue)
            return GetKeyValueCompletions(keyValue, position);

        var children = node.GetChildren();

        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            var nextChild = i < children.Length - 1
                                ? children[i + 1]
                                : null;

            var start = child.Span.Start;
            var end = nextChild == null ? child.Span.End : nextChild.Span.Start;

            if (start <= position && position <= end)
                if (child is QuerySyntax expression)
                    return Complete(expression, position);
                else
                    return null;
        }

        return GetKeywordCompletions(position);
    }

    private QueryCompletionResult GetTextCompletions(TextQuerySyntax text)
    {
        Debug.Assert(text.TextToken.Value is not null);

        var completions = GetCompletionsForText(text.TextToken.Value);
        return new QueryCompletionResult(completions, text.TextToken.Span);
    }

    private QueryCompletionResult GetKeyValueCompletions(KeyValueQuerySyntax keyValue, int position)
    {
        Debug.Assert(keyValue.KeyToken.Value is not null);

        if (position < keyValue.ColonToken.Span.End)
        {
            var completions = GetCompletionsForText(keyValue.KeyToken.Value);
            return new QueryCompletionResult(completions, keyValue.KeyToken.Span);
        }
        else
        {
            Debug.Assert(keyValue.ValueToken.Value is not null);
            var completions = GetCompletionForKeyValue(keyValue.KeyToken.Value, keyValue.ValueToken.Value);
            return new QueryCompletionResult(completions, keyValue.ValueToken.Span);
        }
    }

    private QueryCompletionResult GetKeywordCompletions(int position)
    {
        var completions = GetCompletionsForText(string.Empty);
        return new QueryCompletionResult(completions, TextSpan.FromBounds(position, position));
    }

    public virtual IEnumerable<string> GetCompletionForKeyValue(string key, string value) => Array.Empty<string>();

    public virtual IEnumerable<string> GetCompletionsForText(string text) => Array.Empty<string>();

    private sealed class EmptyQueryCompletionProvider : QueryCompletionProvider
    {
    }
}
