namespace IssuesOfDotNet.Querying
{
    public sealed class OrQuerySyntax : QuerySyntax
    {
        public OrQuerySyntax(QuerySyntax left, QueryToken operatorToken, QuerySyntax right)
        {
            Left = left;
            OperatorToken = operatorToken;
            Right = right;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.OrQuery;
        public override TextSpan Span => TextSpan.FromBounds(Left.Span.Start, Right.Span.End);
        public QuerySyntax Left { get; }
        public QueryToken OperatorToken { get; }
        public QuerySyntax Right { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new QueryNodeOrToken[] { Left, OperatorToken, Right };
        }
    }
}
