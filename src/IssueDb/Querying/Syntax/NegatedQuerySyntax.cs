namespace IssueDb.Querying.Syntax;

public sealed class NegatedQuerySyntax : QuerySyntax
{
    public NegatedQuerySyntax(QueryToken notToken, QuerySyntax query)
    {
        OperatorToken = notToken;
        Query = query;
    }

    public override QuerySyntaxKind Kind => QuerySyntaxKind.NegatedQuery;
    public override TextSpan Span => TextSpan.FromBounds(OperatorToken.Span.Start, Query.Span.End);
    public QueryToken OperatorToken { get; }
    public QuerySyntax Query { get; }

    public override QueryNodeOrToken[] GetChildren()
    {
        return new QueryNodeOrToken[] { OperatorToken, Query };
    }
}
