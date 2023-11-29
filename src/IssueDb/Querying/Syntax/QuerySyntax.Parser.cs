namespace IssueDb.Querying.Syntax;

public partial class QuerySyntax
{
    private sealed class Parser
    {
        private readonly QueryToken[] _tokens;
        private int _tokenIndex;

        public Parser(IEnumerable<QueryToken> tokens)
        {
            _tokens = tokens.ToArray();
        }

        private QueryToken Current => _tokens[_tokenIndex];

        private QueryToken Lookahead => _tokenIndex < _tokens.Length - 1
                                            ? _tokens[_tokenIndex + 1]
                                            : _tokens[_tokens.Length - 1];

        private QueryToken Next()
        {
            var result = Current;
            _tokenIndex++;
            return result;
        }

        private QueryToken Match(QuerySyntaxKind kind)
        {
            if (Current.Kind == kind)
                return Next();

            var token = new QueryToken(kind, Current.QueryText, new TextSpan(Current.Span.End, 0), string.Empty);
            return token;
        }

        private QueryToken MatchTextOrQuotedText()
        {
            var isText = Current.Kind == QuerySyntaxKind.TextToken ||
                         Current.Kind == QuerySyntaxKind.QuotedTextToken;
            return isText ? Next() : Match(QuerySyntaxKind.TextToken);
        }

        private void MarkCurrentAsText()
        {
            if (Current.Kind == QuerySyntaxKind.EndOfFile)
                return;

            _tokens[_tokenIndex] = _tokens[_tokenIndex].AsText();
        }

        public QuerySyntax Parse()
        {
Again:
            var result = ParseExpression();
            if (Current.Kind != QuerySyntaxKind.EndOfFile)
            {
                MarkCurrentAsText();
                _tokenIndex = 0;
                goto Again;
            }

            return result;
        }

        private QuerySyntax ParseExpression()
        {
            return ParseOrExpression();
        }

        private QuerySyntax ParseOrExpression()
        {
            var result = ParseAndExpression();
            while (Current.Kind == QuerySyntaxKind.OrKeyword)
            {
                var operatorToken = Next();
                var term = ParseAndExpression();
                result = new OrQuerySyntax(result, operatorToken, term);
            }

            return result;
        }

        private QuerySyntax ParseAndExpression()
        {
            var result = ParsePrimaryExpression();
            while (Current.Kind != QuerySyntaxKind.EndOfFile &&
                   Current.Kind != QuerySyntaxKind.OrKeyword &&
                   Current.Kind != QuerySyntaxKind.CloseParenthesisToken)
            {
                var term = ParsePrimaryExpression();
                result = new AndQuerySyntax(result, term);
            }

            return result;
        }

        private QuerySyntax ParsePrimaryExpression()
        {
            return Current.Kind switch
            {
                QuerySyntaxKind.NotKeyword => ParseNotExpression(),
                QuerySyntaxKind.OpenParenthesisToken => ParseParenthesizedExpression(),
                _ => ParseTextOrKeyValueExpression(),
            };
        }

        private QuerySyntax ParseNotExpression()
        {
            if (!CanStartPrimaryExpression(Lookahead.Kind))
            {
                MarkCurrentAsText();
                return ParseTextOrKeyValueExpression();
            }

            var token = Next();
            if (Current.Kind == QuerySyntaxKind.EndOfFile)
            {
                token = token.AsText();
                return new TextQuerySyntax(token);
            }

            var expression = ParsePrimaryExpression();
            return new NegatedQuerySyntax(token, expression);
        }

        private QuerySyntax ParseTextOrKeyValueExpression()
        {
            if (Current.Kind == QuerySyntaxKind.TextToken &&
                Lookahead.Kind == QuerySyntaxKind.ColonToken)
            {
                var key = Current;
                var colon = Lookahead;

                // If there is whitespace before the colon, we treat the colon
                // as text.

                if (key.Span.End >= colon.Span.Start)
                    return ParseKeyValueExpression();

                _tokens[_tokenIndex + 1] = colon.AsText();
            }

            // If the current token isn't text, we make it text.
            // This is to avoid an infinite loop in the parser
            // where we keep inserting new tokens when we can't
            // parse a primary expression.

            MarkCurrentAsText();
            return ParseTextExpression();
        }

        private QuerySyntax ParseTextExpression()
        {
            var token = MatchTextOrQuotedText();
            return new TextQuerySyntax(token);
        }

        private QuerySyntax ParseKeyValueExpression()
        {
            var key = Match(QuerySyntaxKind.TextToken);
            var colon = Match(QuerySyntaxKind.ColonToken);
            var value = ReadKeyValueArgument(colon);
            return new KeyValueQuerySyntax(key, colon, value);
        }

        private QueryToken ReadKeyValueArgument(QueryToken colon)
        {
            if (Current.Span.Start > colon.Span.End)
                return new QueryToken(QuerySyntaxKind.TextToken, colon.QueryText, new TextSpan(colon.Span.End, 0), string.Empty);

            if (Current.Kind == QuerySyntaxKind.QuotedTextToken)
                return Match(QuerySyntaxKind.QuotedTextToken);

            var start = Current.Span.Start;
            var end = start;

            while (Current.Span.Start == end && CanFollowColon(Current.Kind))
            {
                MarkCurrentAsText();
                end = Current.Span.End;
                Next();
            }

            var queryText = Current.QueryText;
            var span = TextSpan.FromBounds(start, end);
            var value = queryText.Substring(span.Start, span.Length);
            return new QueryToken(QuerySyntaxKind.TextQuery, queryText, span, value);
        }

        private QuerySyntax ParseParenthesizedExpression()
        {
            var openParenthesisToken = Match(QuerySyntaxKind.OpenParenthesisToken);
            var expression = ParseExpression();
            var closeParenthesisToken = Match(QuerySyntaxKind.CloseParenthesisToken);
            return new ParenthesizedQuerySyntax(openParenthesisToken, expression, closeParenthesisToken);
        }

        private static bool CanStartPrimaryExpression(QuerySyntaxKind kind)
        {
            switch (kind)
            {
                case QuerySyntaxKind.OpenParenthesisToken:
                case QuerySyntaxKind.TextToken:
                case QuerySyntaxKind.NotKeyword:
                case QuerySyntaxKind.QuotedTextToken:
                    return true;
                default:
                    return false;
            }
        }

        private static bool CanFollowColon(QuerySyntaxKind kind)
        {
            switch (kind)
            {
                case QuerySyntaxKind.TextToken:
                case QuerySyntaxKind.ColonToken:
                    return true;
                default:
                    return false;
            }
        }
    }
}
