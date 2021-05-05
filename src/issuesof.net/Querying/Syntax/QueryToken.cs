using System;

namespace IssuesOfDotNet.Querying
{
    public sealed class QueryToken : QueryNodeOrToken
    {
        public QueryToken(QuerySyntaxKind kind, string queryText, TextSpan span, string value)
        {
            Kind = kind;
            QueryText = queryText;
            Span = span;
            Value = value;
        }

        public override QuerySyntaxKind Kind { get; }
        public override TextSpan Span { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return Array.Empty<QueryNodeOrToken>();
        }

        public string QueryText { get; }
        public string Text => QueryText.Substring(Span.Start, Span.Length);
        public string Value { get; }

        public override string ToString()
        {
            return Text;
        }

        public QueryToken AsText()
        {
            if (Kind == QuerySyntaxKind.TextToken ||
                Kind == QuerySyntaxKind.QuotedTextToken)
                return this;

            var value = QueryText.Substring(Span.Start, Span.Length);
            return new QueryToken(QuerySyntaxKind.TextToken, QueryText, Span, value);
        }
    }
}
