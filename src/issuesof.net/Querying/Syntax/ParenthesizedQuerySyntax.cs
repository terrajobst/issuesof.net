namespace IssuesOfDotNet.Querying
{
    public sealed class ParenthesizedQuerySyntax : QuerySyntax
    {
        public ParenthesizedQuerySyntax(QueryToken openParenthesisToken, QuerySyntax query, QueryToken closeParenthesisToken)
        {
            OpenParenthesisToken = openParenthesisToken;
            Query = query;
            CloseParenthesisToken = closeParenthesisToken;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.ParenthesizedQuery;
        public override TextSpan Span => TextSpan.FromBounds(OpenParenthesisToken.Span.Start, CloseParenthesisToken.Span.End);
        public QueryToken OpenParenthesisToken { get; }
        public QuerySyntax Query { get; }
        public QueryToken CloseParenthesisToken { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new QueryNodeOrToken[] { OpenParenthesisToken, Query, CloseParenthesisToken };
        }
    }
}
