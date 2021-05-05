using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IssuesOfDotNet.Querying
{
    public static class QuerySyntax
    {
        public static ExpressionSyntax Parse(string text)
        {
            var tokens = Lexer.Tokenize(text)
                              .Where(t => t.Kind != QuerySyntaxKind.WhitespaceToken);
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        private static class Lexer
        {
            public static IEnumerable<QueryToken> Tokenize(string text)
            {
                var position = 0;

                while (position < text.Length)
                {
                    var c = text[position];
                    switch (c)
                    {
                        case '(':
                            yield return ReadSingleCharacterToken(QuerySyntaxKind.OpenParenthesisToken, ref position, text);
                            break;
                        case ')':
                            yield return ReadSingleCharacterToken(QuerySyntaxKind.CloseParenthesisToken, ref position, text);
                            break;
                        case ':':
                            yield return ReadSingleCharacterToken(QuerySyntaxKind.ColonToken, ref position, text);
                            break;
                        case '-':
                            yield return ReadSingleCharacterToken(QuerySyntaxKind.NotKeyword, ref position, text);
                            break;

                        case '"':
                            yield return ReadQuotedText(ref position, text);
                            break;

                        default:
                            if (char.IsWhiteSpace(c))
                                yield return ReadWhitespace(ref position, text);
                            else
                                yield return ReadText(ref position, text);
                            break;
                    }
                }

                yield return new QueryToken(QuerySyntaxKind.EndOfFile, text, new TextSpan(position, 0), null);
            }

            private static QueryToken ReadSingleCharacterToken(QuerySyntaxKind kind, ref int position, string text)
            {
                var span = new TextSpan(position, 1);
                var token = new QueryToken(kind, text, span, null);
                position += span.Length;
                return token;
            }

            private static QueryToken ReadWhitespace(ref int position, string text)
            {
                var start = position;

                while (position < text.Length && char.IsWhiteSpace(text[position]))
                    position++;

                var length = position - start;
                var span = new TextSpan(start, length);
                return new QueryToken(QuerySyntaxKind.WhitespaceToken, text, span, null);
            }

            private static QueryToken ReadQuotedText(ref int position, string text)
            {
                var start = position;
                position++; // skip initial quote
                var sb = new StringBuilder();

                while (position < text.Length)
                {
                    var c = text[position];
                    var l = position < text.Length - 1
                                ? text[position + 1]
                                : '\0';

                    if (c == '"')
                    {
                        position++;

                        if (l != '"')
                            break;
                    }

                    sb.Append(c);
                    position++;
                }

                var length = position - start;
                var span = new TextSpan(start, length);
                var value = sb.ToString();
                return new QueryToken(QuerySyntaxKind.QuotedTextToken, text, span, value);
            }

            private static QueryToken ReadText(ref int position, string text)
            {
                var start = position;

                while (position < text.Length)
                {
                    var c = text[position];
                    if (c == ':' || c == '(' || c == ')' || char.IsWhiteSpace(c))
                        break;

                    position++;
                }

                var length = position - start;
                var span = new TextSpan(start, length);

                var tokenText = text.Substring(start, length);
                var kind = GetKeywordOrText(tokenText);
                return new QueryToken(kind, text, span, tokenText);
            }

            private static QuerySyntaxKind GetKeywordOrText(string text)
            {
                return text.ToLowerInvariant() switch
                {
                    "not" => QuerySyntaxKind.NotKeyword,
                    "or" => QuerySyntaxKind.OrKeyword,
                    _ => QuerySyntaxKind.TextToken,
                };
            }
        }

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

            public ExpressionSyntax Parse()
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

            private ExpressionSyntax ParseExpression()
            {
                return ParseOrExpression();
            }

            private ExpressionSyntax ParseOrExpression()
            {
                var result = ParseAndExpression();
                while (Current.Kind == QuerySyntaxKind.OrKeyword)
                {
                    var operatorToken = Next();
                    var term = ParseAndExpression();
                    result = new OrExpressionSyntax(result, operatorToken, term);
                }

                return result;
            }

            private ExpressionSyntax ParseAndExpression()
            {
                var result = ParsePrimaryExpression();
                while (Current.Kind != QuerySyntaxKind.EndOfFile &&
                       Current.Kind != QuerySyntaxKind.OrKeyword &&
                       Current.Kind != QuerySyntaxKind.CloseParenthesisToken)
                {
                    var term = ParsePrimaryExpression();
                    result = new AndExpressionSyntax(result, term);
                }

                return result;
            }

            private ExpressionSyntax ParsePrimaryExpression()
            {
                return Current.Kind switch
                {
                    QuerySyntaxKind.NotKeyword => ParseNotExpression(),
                    QuerySyntaxKind.OpenParenthesisToken => ParseParenthesizedExpression(),
                    _ => ParseTextOrKeyValueExpression(),
                };
            }

            private ExpressionSyntax ParseNotExpression()
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
                    return new TextExpressionSyntax(token);
                }

                var expression = ParsePrimaryExpression();
                return new NegatedExpressionSyntax(token, expression);
            }

            private ExpressionSyntax ParseTextOrKeyValueExpression()
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

            private ExpressionSyntax ParseTextExpression()
            {
                var token = MatchTextOrQuotedText();
                return new TextExpressionSyntax(token);
            }

            private ExpressionSyntax ParseKeyValueExpression()
            {
                var key = Match(QuerySyntaxKind.TextToken);
                var colon = Match(QuerySyntaxKind.ColonToken);
                var value = ReadKeyValueArgument(colon);
                return new KeyValueExpressionSyntax(key, colon, value);
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
                return new QueryToken(QuerySyntaxKind.TextExpression, queryText, span, value);
            }

            private ExpressionSyntax ParseParenthesizedExpression()
            {
                var openParenthesisToken = Match(QuerySyntaxKind.OpenParenthesisToken);
                var expression = ParseExpression();
                var closeParenthesisToken = Match(QuerySyntaxKind.CloseParenthesisToken);
                return new ParenthesizedExpressionSyntax(openParenthesisToken, expression, closeParenthesisToken);
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
}
