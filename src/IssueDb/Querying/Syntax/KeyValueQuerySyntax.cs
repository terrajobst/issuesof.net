﻿namespace IssueDb.Querying.Syntax;

public sealed class KeyValueQuerySyntax : QuerySyntax
{
    public KeyValueQuerySyntax(QueryToken keyToken, QueryToken colonToken, QueryToken valueToken)
    {
        KeyToken = keyToken;
        ColonToken = colonToken;
        ValueToken = valueToken;
    }

    public override QuerySyntaxKind Kind => QuerySyntaxKind.KeyValueQuery;
    public override TextSpan Span => TextSpan.FromBounds(KeyToken.Span.Start, ValueToken.Span.End);
    public QueryToken KeyToken { get; }
    public QueryToken ColonToken { get; }
    public QueryToken ValueToken { get; }

    public override QueryNodeOrToken[] GetChildren()
    {
        return new[] { KeyToken, ColonToken, ValueToken };
    }
}
