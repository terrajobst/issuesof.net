namespace IssuesOfDotNet.Querying
{
    public abstract class QueryNodeOrToken
    {
        public abstract QuerySyntaxKind Kind { get; }
        public abstract TextSpan Span { get; }
        public abstract QueryNodeOrToken[] GetChildren();
    }
}
