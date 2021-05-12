using System;
using System.Collections.Generic;
using System.Linq;

using IssueDb.Querying.Completion;

namespace IssueDb.Crawling
{
    public sealed class CrawledIndexCompletionProvider : QueryCompletionProvider
    {
        private readonly string[] _keywords;
        private readonly string[] _orgs;
        private readonly string[] _repos;
        private readonly string[] _isValues;
        private readonly string[] _noValues;
        private readonly string[] _draftValues;
        private readonly string[] _typeValues;
        private readonly string[] _stateValues;
        private readonly string[] _archivedValues;
        private readonly string[] _users;
        private readonly string[] _labels;
        private readonly string[] _milestones;
        private readonly string[] _areaPaths;
        private readonly string[] _areaNodes;
        private readonly string[] _sortValues;

        public CrawledIndexCompletionProvider(CrawledIndex index)
        {
            _keywords = new[] {
                "archived",
                "area",
                "area-node",
                "area-under",
                "assignee",
                "author",
                "closed",
                "comments",
                "created",
                "draft",
                "interactions",
                "is",
                "label",
                "milestone",
                "no",
                "org",
                "reactions",
                "repo",
                "sort",
                "state",
                "type",
                "updated"
            };

            _orgs = new SortedSet<string>(
                 index.Repos.Select(r => r.Org),
                 StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _repos = new SortedSet<string>(
                 index.Repos.SelectMany(r => new[] { r.Name, r.FullName }),
                 StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _isValues = new[] { "closed", "draft", "issue", "open", "merged", "pr", "unmerged" };
            _noValues = new[] { "area", "assignee", "label", "milestone" };
            _draftValues = new[] { "false", "true" };
            _typeValues = new[] { "issue", "pr" };
            _stateValues = new[] { "closed", "merged", "open", "unmerged" };
            _archivedValues = new[] { "false", "true" };

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
                           .Select(m => m.Title),
                StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _areaPaths = new SortedSet<string>(
                index.Repos.SelectMany(r => r.Labels)
                           .SelectMany(l => TextTokenizer.GetAreaPaths(l.Name)),
                StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _areaNodes = new SortedSet<string>(
                index.Repos.SelectMany(r => r.Labels)
                           .SelectMany(l => TextTokenizer.GetAreaPaths(l.Name, segmentsOnly: true)),
                StringComparer.OrdinalIgnoreCase
            ).ToArray();

            _sortValues = new[] {
                "comments",
                "comments-asc",
                "comments-desc",
                "created",
                "created-asc",
                "created-desc",
                "interactions",
                "interactions-asc",
                "interactions-desc",
                "reactions",
                "reactions-+1",
                "reactions-+1-asc",
                "reactions-+1-desc",
                "reactions--1",
                "reactions--1-asc",
                "reactions--1-desc",
                "reactions-asc",
                "reactions-desc",
                "reactions-heart",
                "reactions-heart-asc",
                "reactions-heart-desc",
                "reactions-smile",
                "reactions-smile-asc",
                "reactions-smile-desc",
                "reactions-tada",
                "reactions-tada-asc",
                "reactions-tada-desc",
                "reactions-thinking_face",
                "reactions-thinking_face-asc",
                "reactions-thinking_face-desc",
                "updated",
                "updated-asc",
                "updated-desc"
            };
        }

        public override IEnumerable<string> GetCompletionForKeyValue(string key, string value)
        {
            var completions = key.ToLowerInvariant() switch
            {
                "org" => _orgs,
                "repo" => _repos,
                "is" => _isValues,
                "type" => _typeValues,
                "state" => _stateValues,
                "draft" => _draftValues,
                "archived" => _archivedValues,
                "no" => _noValues,
                "author" => _users,
                "assignee" => _users,
                "label" => _labels,
                "milestone" => _milestones,
                "area" or "area-under" => _areaPaths,
                "area-node" => _areaNodes,
                "sort" => _sortValues,
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

        public override IEnumerable<string> GetCompletionsForText(string text)
        {
            var index = Array.BinarySearch(_keywords, text, StringComparer.OrdinalIgnoreCase);
            if (index < 0)
                index = ~index;

            for (var i = index; i < _keywords.Length; i++)
            {
                var keyword = _keywords[i];
                if (!keyword.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    yield break;

                yield return keyword;
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
