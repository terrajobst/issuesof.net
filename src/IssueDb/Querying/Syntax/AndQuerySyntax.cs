namespace IssueDb.Querying.Syntax
{
    public sealed class AndQuerySyntax : QuerySyntax
    {
        public AndQuerySyntax(QuerySyntax left, QuerySyntax right)
        {
            Left = left;
            Right = right;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.AndQuery;
        public override TextSpan Span => TextSpan.FromBounds(Left.Span.Start, Right.Span.End);
        public QuerySyntax Left { get; }
        public QuerySyntax Right { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new[] { Left, Right };
        }
    }
}
