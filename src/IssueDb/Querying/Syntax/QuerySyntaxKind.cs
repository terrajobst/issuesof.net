namespace IssueDb.Querying.Syntax;

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
