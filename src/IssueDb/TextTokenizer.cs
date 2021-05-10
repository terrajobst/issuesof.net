using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        public static IReadOnlyList<string> GetAreaPaths(string label)
        {
            const string prefix = "area-";
            if (!label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();

            var result = new List<string>();
            var remainder = label.Substring(prefix.Length);
            var wasSeparator = true;
            for (var i = 0; i < remainder.Length; i++)
            {
                var c = remainder[i];
                var isAreaPathText = char.IsLetterOrDigit(c) ||
                                     char.IsWhiteSpace(c);

                if (isAreaPathText)
                    wasSeparator = false;
                else if (!wasSeparator)
                    result.Add(remainder.Substring(0, i));
            }

            result.Add(remainder);
            return result;
        }
    }
}
