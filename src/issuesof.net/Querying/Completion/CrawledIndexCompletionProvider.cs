using System;
using System.Collections.Generic;
using System.Linq;

namespace IssuesOfDotNet.Querying
{
    public sealed class CrawledIndexCompletionProvider : QueryCompletionProvider
    {
        private readonly string[] _orgs;
        private readonly string[] _repos;
        private readonly string[] _isValues;
        private readonly string[] _noValues;
        private readonly string[] _users;
        private readonly string[] _labels;
        private readonly string[] _milestones;

        public CrawledIndexCompletionProvider(CrawledIndex index)
        {
            _orgs = new SortedSet<string>(
                 index.Repos.Select(r => r.Org),
                 StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _repos = new SortedSet<string>(
                 index.Repos.Select(r => r.Name),
                 StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _isValues = new[] { "closed", "issue", "open", "merged", "pr" };
            _noValues = new[] { "assignee", "label", "milestone" };

            _users = new SortedSet<string>(
                 index.Repos.SelectMany(r => r.Issues.Values)
                            .SelectMany(i => new[] { i.CreatedBy }.Concat(i.Assignees)),
                 StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _labels = new SortedSet<string>(
                index.Repos.SelectMany(r => r.Labels)
                           .Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _milestones = new SortedSet<string>(
                index.Repos.SelectMany(r => r.Milestones)
                           .Select(m=> m.Title),
                StringComparer.OrdinalIgnoreCase
            ).ToArray();
        }

        public override IEnumerable<string> GetCompletionForKeyValue(string key, string value)
        {
            var completions = key.ToLowerInvariant() switch
            {
                "org" => _orgs,
                "repo" => _repos,
                "is" => _isValues,
                "no" => _noValues,
                "author" => _users,
                "assignee" => _users,
                "label" => _labels,
                "milestone" => _milestones,
                _ => Array.Empty<string>(),
            };

            var index = Array.BinarySearch(completions, value, StringComparer.OrdinalIgnoreCase);
            if (index < 0)
                index = ~index;

            for (var i = index; i < completions.Length; i++)
            {
                var c = completions[i];
                if (!c.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                    yield break;

                yield return Escape(c);
            }
        }

        // TODO: Do we need to escape anything else?
        private static string Escape(string text)
        {
            if (!text.Contains(" "))
                return text;

            return $"\"{text}\"";
        }
    }
}
