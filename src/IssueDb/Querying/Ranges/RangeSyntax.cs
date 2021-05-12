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
            return RangeSyntax<DateTimeOffset>.Parse(text, DateTimeOffset.TryParse);
        }
    }
}
