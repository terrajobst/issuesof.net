namespace IssuesOfDotNet.Querying
{
    public sealed class OrExpressionSyntax : ExpressionSyntax
    {
        public OrExpressionSyntax(ExpressionSyntax left, QueryToken operatorToken, ExpressionSyntax right)
        {
            Left = left;
            OperatorToken = operatorToken;
            Right = right;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.OrExpression;
        public override TextSpan Span => TextSpan.FromBounds(Left.Span.Start, Right.Span.End);
        public ExpressionSyntax Left { get; }
        public QueryToken OperatorToken { get; }
        public ExpressionSyntax Right { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new QueryNodeOrToken[] { Left, OperatorToken, Right };
        }
    }
}
