using System.Collections.Generic;

namespace IssuesOfDotNet.Querying
{
    public sealed class QueryCompletionResult
    {
        public QueryCompletionResult(IEnumerable<string> completions, TextSpan span)
        {
            Completions = completions;
            Span = span;
        }

        public IEnumerable<string> Completions { get; }
        public TextSpan Span { get; }
    }
}
