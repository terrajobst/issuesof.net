namespace IssuesOfDotNet.Querying
{
    public sealed class AndExpressionSyntax : ExpressionSyntax
    {
        public AndExpressionSyntax(ExpressionSyntax left, ExpressionSyntax right)
        {
            Left = left;
            Right = right;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.AndExpression;
        public override TextSpan Span => TextSpan.FromBounds(Left.Span.Start, Right.Span.End);
        public ExpressionSyntax Left { get; }
        public ExpressionSyntax Right { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new[] { Left, Right };
        }
    }
}
