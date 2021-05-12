using System;

namespace IssueDb.Querying.Ranges
{
    public static class RangeSyntax
    {
        public static RangeSyntax<int> ParseInt32(string text)
        {
            return RangeSyntax<int>.Parse(text, int.TryParse);
        }

        public static RangeSyntax<DateTimeOffset> ParseDateTimeOffset(string text)
        {
            return RangeSyntax<DateTimeOffset>.Parse(text, TryParseDateTime);

            static bool TryParseDateTime(string text, out DateTimeOffset value)
            {
                if (text is null)
                {
                    value = default;
                    return false;
                }

                if (string.Equals(text.Trim(), "@today", StringComparison.OrdinalIgnoreCase))
                {
                    value = DateTimeOffset.UtcNow;
                    return true;
                }

                var ops = new[] { "-", "+" };
                foreach (var op in ops)
                {
                    var indexOfOp = text.IndexOf(op, StringComparison.OrdinalIgnoreCase);
                    if (indexOfOp > 0)
                    {
                        var leftText = text.Substring(0, indexOfOp).Trim();
                        var rightText = text.Substring(indexOfOp + op.Length).Trim();

                        if (string.Equals(leftText, "@today", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(rightText, out var days))
                        {
                            if (op == "-")
                                days = -days;

                            value = DateTimeOffset.UtcNow.AddDays(days);
                            return true;
                        }
                    }
                }

                return DateTimeOffset.TryParse(text, out value);
            }
        }
    }
}
