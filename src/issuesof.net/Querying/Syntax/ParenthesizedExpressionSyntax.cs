namespace IssuesOfDotNet.Querying
{
    public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
    {
        public ParenthesizedExpressionSyntax(QueryToken openParenthesisToken, ExpressionSyntax expression, QueryToken closeParenthesisToken)
        {
            OpenParenthesisToken = openParenthesisToken;
            Expression = expression;
            CloseParenthesisToken = closeParenthesisToken;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.ParenthesizedExpression;
        public override TextSpan Span => TextSpan.FromBounds(OpenParenthesisToken.Span.Start, CloseParenthesisToken.Span.End);
        public QueryToken OpenParenthesisToken { get; }
        public ExpressionSyntax Expression { get; }
        public QueryToken CloseParenthesisToken { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new QueryNodeOrToken[] { OpenParenthesisToken, Expression, CloseParenthesisToken };
        }
    }
}
