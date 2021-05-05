namespace IssuesOfDotNet.Querying
{
    public sealed class NegatedExpressionSyntax : ExpressionSyntax
    {
        public NegatedExpressionSyntax(QueryToken notToken, ExpressionSyntax expression)
        {
            OperatorToken = notToken;
            Expression = expression;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.NegatedExpression;
        public override TextSpan Span => TextSpan.FromBounds(OperatorToken.Span.Start, Expression.Span.End);
        public QueryToken OperatorToken { get; }
        public ExpressionSyntax Expression { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new QueryNodeOrToken[] { OperatorToken, Expression };
        }
    }
}
