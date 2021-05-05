namespace IssuesOfDotNet.Querying
{
    public sealed class TextQuerySyntax : QuerySyntax
    {
        public TextQuerySyntax(QueryToken textToken)
        {
            TextToken = textToken;
        }

        public override QuerySyntaxKind Kind => QuerySyntaxKind.TextQuery;
        public override TextSpan Span => TextToken.Span;
        public QueryToken TextToken { get; }

        public override QueryNodeOrToken[] GetChildren()
        {
            return new[] { TextToken };
        }
    }
}
