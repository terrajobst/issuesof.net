using System;
using System.Collections.Generic;
using System.Linq;

using Markdig;

namespace IssuesOfDotNet
{
    // TODO: We need to compactify this data structure
    //
    // Couple of ideas:
    // - We should consider a flat buffer representation
    // - We should intern strings
    // - We should only store necessary information, e.g. not issue bodies
    //
    // TODO: We should think about normalization of text
    //
    // - For example, we should include ".NET" and "C#", but we should generally strip punctuation.
    // - We should also consider breaking pascal case as individual words.

    public sealed class CrawledTrie
    {
        public CrawledTrie()
            : this(new CrawledTrieNode())
        {
        }

        public CrawledTrie(CrawledTrieNode root)
        {
            Root = root;
        }

        public void AddIssue(CrawledIssue issue)
        {
            var terms = GetTerms(issue);

            foreach (var term in terms)
            {
                if (!Skip(term))
                    AddIssue(term, issue);
            }
        }

        private static bool Skip(string term)
        {
            // Let's skip really short words
            return term.Length < 3;
        }

        private static IEnumerable<string> GetTerms(CrawledIssue issue)
        {
            var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTermsFromMarkdown(result, issue.Title);
            // TODO: Should we index the body or not?
            // AddTermsFromMarkdown(result, issue.Body);

            result.Add("org:" + issue.Org);
            result.Add("repo:" + issue.Repo);
            result.Add("author:" + issue.CreatedBy);

            foreach (var assignee in issue.Assignees)
                result.Add("assignee:" + assignee);

            foreach (var label in issue.Labels)
                result.Add("label:" + label.Name);

            if (issue.Milestone is not null)
                result.Add("milestone:" + issue.Milestone.Title);

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

            var tokens = Tokenize(text);
            foreach (var token in tokens)
                target.Add(token);
        }

        private static string[] Tokenize(string text)
        {
            // TODO: Fix handling of puncuation
            //
            // We want to index tokens like ".NET" but we don't want to index comma or periods
            // at the end of words.
            //
            // Also, if a word starts with '<' we don't want to index it.
            //
            // If a word only contains punctuation, we don't want to index it.

            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            var result = new List<string>();
            var start = -1;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (!char.IsWhiteSpace(c))
                {
                    if (start < 0)
                        start = i;
                }
                else if (start >= 0 && i > start)
                {
                    var word = text[start..i];
                    result.Add(word);
                    start = -1;
                }
            }

            if (start >= 0 && start < text.Length - 1)
                result.Add(text[start..]);

            return result.ToArray();
        }

        public CrawledTrieLookupResult LookupIssues(string query)
        {
            var terms = Tokenize(query);
            var issues = LookupIssues(terms);
            return new CrawledTrieLookupResult(issues);
        }

        private IReadOnlyCollection<CrawledIssue> LookupIssues(IEnumerable<string> terms)
        {
            var result = (HashSet<CrawledIssue>)null;

            foreach (var term in terms)
            {
                if (Skip(term))
                    continue;

                var node = LookupNode(term);

                if (node is null)
                    return Array.Empty<CrawledIssue>();

                if (result is null)
                    result = node.Issues.ToHashSet();
                else
                    result.IntersectWith(node.Issues);
            }

            return (IReadOnlyCollection<CrawledIssue>)result ?? Array.Empty<CrawledIssue>();
        }

        public CrawledTrieNode LookupNode(string text)
        {
            return Walk(text, addNodes: false);
        }

        private CrawledTrieNode Walk(string text, bool addNodes)
        {
            text = text.ToLowerInvariant();

            var current = Root;
            var textStart = 0;

            while (textStart < text.Length)
            {
                var remaining = text[textStart..];

                var childIndex = BinarySearch(current, remaining);

                if (childIndex >= 0)
                {
                    current = current.Children[childIndex];
                    textStart += current.Text.Length;
                    continue;
                }

                if (!addNodes)
                    return null;

                childIndex = ~childIndex;

                var previousChild = childIndex == 0
                    ? null
                    : current.Children[childIndex - 1];

                var commonPrefixLength = GetCommonPrefix(previousChild?.Text, remaining);
                if (commonPrefixLength > 0)
                {
                    var prefixChild = new CrawledTrieNode()
                    {
                        Text = previousChild.Text.Substring(0, commonPrefixLength)
                    };

                    previousChild.Text = previousChild.Text.Substring(commonPrefixLength);
                    prefixChild.InsertChild(0, previousChild);
                    current.ReplaceChild(childIndex - 1, prefixChild);

                    current = prefixChild;
                    textStart += commonPrefixLength;
                    continue;
                }
                else
                {
                    var nextChild = childIndex == current.Children.Count
                        ? null
                        : current.Children[childIndex];

                    commonPrefixLength = GetCommonPrefix(nextChild?.Text, remaining);

                    if (commonPrefixLength > 0)
                    {
                        var prefixChild = new CrawledTrieNode()
                        {
                            Text = nextChild.Text.Substring(0, commonPrefixLength)
                        };

                        nextChild.Text = nextChild.Text.Substring(commonPrefixLength);
                        prefixChild.InsertChild(0, nextChild);
                        current.ReplaceChild(childIndex, prefixChild);

                        current = prefixChild;
                        textStart += commonPrefixLength;
                        continue;
                    }
                    else
                    {
                        var newChild = new CrawledTrieNode
                        {
                            Text = remaining
                        };
                        current.InsertChild(childIndex, newChild);
                        return newChild;
                    }
                }
            }

            return current;
        }

        private static int GetCommonPrefix(string text1, string text2)
        {
            var prefixLength = 0;

            if (text1 is not null && text2 is not null)
            {
                var maxLength = Math.Min(text1.Length, text2.Length);
                var position = 0;
                while (position < maxLength && text1[position] == text2[position])
                {
                    prefixLength++;
                    position++;
                }
            }

            return prefixLength;
        }

        public void AddIssue(string text, CrawledIssue issue)
        {
            var current = Walk(text, addNodes: true);
            current.AddIssue(issue);
        }

        private static int BinarySearch(CrawledTrieNode node, string text)
        {
            var lo = 0;
            var hi = node.Children.Count - 1;

            while (lo <= hi)
            {
                var i = (lo + hi) / 2;

                var childText = node.Children[i].Text;
                var length = text.Length;
                if (childText.Length < text.Length)
                    length = childText.Length;

                var c = childText.AsSpan().SequenceCompareTo(text.AsSpan(0, length));
                if (c == 0)
                    return i;

                if (c < 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return ~lo;
        }

        public CrawledTrieNode Root { get; }
    }
}
