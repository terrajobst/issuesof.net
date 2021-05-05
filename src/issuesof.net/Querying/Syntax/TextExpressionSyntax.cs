namespace IssuesOfDotNet.Querying
{
    public sealed class TextExpressionSyntax : ExpressionSyntax
    {
        public TextExpressionSyntax(QueryToken textToken)
        {
            TextToken = textToken;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.TextExpression;
        public override TextSpan Span => TextToken.Span;
        public QueryToken TextToken { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new[] { TextToken };
        }
    }
}
