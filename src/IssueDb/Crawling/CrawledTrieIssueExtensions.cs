using System;
using System.Collections.Generic;

using Markdig;

namespace IssueDb.Crawling
{
    public static class CrawledTrieIssueExtensions
    {
        public static void Add(this CrawledTrie<CrawledIssue> trie, CrawledIssue issue)
        {
            var terms = issue.GetTrieTerms();

            foreach (var term in terms)
                trie.Add(term, issue);
        }

        public static IEnumerable<string> GetTrieTerms(this CrawledIssue issue)
        {
            var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTermsFromMarkdown(result, issue.Title);
            // TODO: Should we index the body or not?
            // AddTermsFromMarkdown(result, issue.Body);

            result.Add($"org:{issue.Repo.Org}");
            result.Add($"repo:{issue.Repo.Name}");
            result.Add($"repo:{issue.Repo.FullName}");
            result.Add($"author:{issue.CreatedBy}");

            foreach (var assignee in issue.Assignees)
                result.Add($"assignee:{assignee}");

            foreach (var label in issue.Labels)
                result.Add($"label:{label.Name}");

            foreach (var area in issue.Areas)
                result.Add($"area-under:{area}");

            foreach (var areaNode in issue.AreaNodes)
                result.Add($"area-node:{areaNode}");

            foreach (var areaLead in issue.AreaLeads)
                result.Add($"area-lead:{areaLead}");

            foreach (var areaOwner in issue.AreaOwners)
                result.Add($"area-owner:{areaOwner}");

            if (issue.Milestone is not null)
                result.Add($"milestone:{issue.Milestone.Title}");

            return result;
        }

        private static void AddTermsFromMarkdown(ISet<string> target, string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return;

            try
            {
                var plainText = Markdown.ToPlainText(markdown);
                AddTermsFromPlainText(target, plainText);
            }
            catch (Exception)
            {
                // If we can't convert the Markdown (e.g. very large table or something)
                // we just give up.
                return;
            }
        }

        private static void AddTermsFromPlainText(ISet<string> target, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var tokens = TextTokenizer.Tokenize(text);
            target.UnionWith(tokens);
        }
    }
}
