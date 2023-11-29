using System;
using System.Collections.Generic;
using System.Linq;

using IssueDb.Querying;
using IssueDb.Querying.Completion;

namespace IssueDb.Crawling;

public sealed class CrawledIndexCompletionProvider : QueryCompletionProvider
{
    private readonly string[] _orgs;
    private readonly string[] _repos;
    private readonly string[] _users;
    private readonly string[] _labels;
    private readonly string[] _milestones;
    private readonly string[] _areaPaths;
    private readonly string[] _areaNodes;
    private readonly string[] _areaPods;

    public CrawledIndexCompletionProvider(CrawledIndex index)
    {
        _orgs = new SortedSet<string>(
             index.Repos.Select(r => r.Org),
             StringComparer.OrdinalIgnoreCase
        ).ToArray();

        _repos = new SortedSet<string>(
             index.Repos.SelectMany(r => new[] { r.Name, r.FullName }),
             StringComparer.OrdinalIgnoreCase
        ).ToArray();

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

        _areaPods = new SortedSet<string>(
            index.Repos.SelectMany(r => r.AreaOwners)
                       .Select(a => a.Value.Pod),
            StringComparer.OrdinalIgnoreCase
        ).ToArray();
    }

    public override IEnumerable<string> GetCompletionForKeyValue(string key, string value)
    {
        var completions = key.ToLowerInvariant() switch
        {
            "org" => _orgs,
            "repo" => _repos,
            "author" or "area-lead" or "area-owner" => _users,
            "assignee" => _users,
            "label" => _labels,
            "milestone" => _milestones,
            "area" or "area-under" => _areaPaths,
            "area-node" => _areaNodes,
            "area-pod" => _areaPods,
            _ => IssueQuery.SupportedValuesFor(key)
                           .OrderBy(x => x)
                           .ToArray()
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
        var keywords = IssueQuery.SupportedKeys
                                 .OrderBy(k => k)
                                 .ToArray();

        var index = Array.BinarySearch(keywords, text, StringComparer.OrdinalIgnoreCase);
        if (index < 0)
            index = ~index;

        for (var i = index; i < keywords.Length; i++)
        {
            var keyword = keywords[i];
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
