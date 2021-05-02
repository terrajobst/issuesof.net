using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IssuesOfDotNet
{
    public sealed class CrawledTrieNode
    {
        public CrawledTrieNode()
        {
        }

        public CrawledTrieNode(string text, IEnumerable<CrawledTrieNode> children, IEnumerable<CrawledIssue> issues)
        {
            Text = text;

            if (children.Any())
                _children = children.ToArray();

            if (issues.Any())
                _issues = issues.ToArray();
        }

        private IList<CrawledTrieNode> _children;
        private IList<CrawledIssue> _issues;

        public string Text { get; set; }

        public void InsertChild(int index, CrawledTrieNode child)
        {
            if (_children is null)
                _children = new List<CrawledTrieNode>();

            _children.Insert(index, child);
        }

        public void RemoveChild(CrawledTrieNode child)
        {
            _children?.Remove(child);
        }

        public void ReplaceChild(int childIndex, CrawledTrieNode child)
        {
            Debug.Assert(_children is not null);
            _children[childIndex] = child;
        }

        public void AddIssue(CrawledIssue issue)
        {
            if (_issues is null)
                _issues = new List<CrawledIssue>();

            _issues.Add(issue);
        }

        public void RemoveIssue(CrawledIssue issue)
        {
            _issues?.Remove(issue);
        }

        public IReadOnlyList<CrawledTrieNode> Children => (IReadOnlyList<CrawledTrieNode>) _children ?? Array.Empty<CrawledTrieNode>();

        public IReadOnlyList<CrawledIssue> Issues => (IReadOnlyList<CrawledIssue>)_issues ?? Array.Empty<CrawledIssue>();
    }
}
