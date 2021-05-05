namespace IssuesOfDotNet.Querying
{
    public enum QuerySyntaxKind
    {
        None,
        EndOfFile,
        OpenParenthesisToken,
        CloseParenthesisToken,
        ColonToken,
        TextToken,
        OrKeyword,
        NotKeyword,
        QuotedTextToken,
        WhitespaceToken,
        TextQuery,
        KeyValueQuery,
        OrQuery,
        AndQuery,
        NegatedQuery,
        ParenthesizedQuery,
    }
}
