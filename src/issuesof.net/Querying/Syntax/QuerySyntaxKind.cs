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
        TextExpression,
        KeyValueExpression,
        OrExpression,
        AndExpression,
        NegatedExpression,
        ParenthesizedExpression,
    }
}
