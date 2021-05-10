using System.Collections.Generic;

using Humanizer;

namespace IssuesOfDotNet
{
    public static class TextTokenizer
    {
        public static IEnumerable<string> Tokenize(string text)
        {
            var result = new SortedSet<string>();

            foreach (var nestedToken in SplitByPunctuationAndCaseChanges(text))
            {
                var normalized = nestedToken.ToLowerInvariant();
                var singular = normalized.Singularize();
                result.Add(normalized);
                result.Add(singular);
            }

            result.RemoveWhere(x => x.Length < 2);

            return result;
        }

        private static IEnumerable<string> SplitByPunctuationAndCaseChanges(string token)
        {
            var position = 0;
            var start = -1;

            while (position < token.Length)
            {
                var c = token[position];
                var l = position < token.Length - 1 ? token[position + 1] : '\0';
                var caseChanged = char.IsLower(c) && char.IsUpper(l);

                if (caseChanged)
                {
                    position++;
                    yield return token[start..position];
                    start = position;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    if (start < 0)
                        start = position;
                    position++;
                }
                else
                {
                    if (start >= 0)
                        yield return token[start..position];

                    start = -1;
                    position++;
                }
            }

            if (start >= 0)
                yield return token[start..];
        }
    }
}
