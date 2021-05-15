using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IssueDb.Crawling
{
    public sealed class CrawledTrieNode<T>
    {
        public CrawledTrieNode()
        {
        }

        public CrawledTrieNode(string text, IEnumerable<CrawledTrieNode<T>> children, IEnumerable<T> values)
        {
            Text = text;

            if (children.Any())
                _children = children.ToArray();

            if (values.Any())
                _values = values.ToArray();
        }

        private IList<CrawledTrieNode<T>> _children;
        private IList<T> _values;

        public string Text { get; set; }

        public void InsertChild(int index, CrawledTrieNode<T> child)
        {
            if (_children is null)
                _children = new List<CrawledTrieNode<T>>();

            _children.Insert(index, child);
        }

        public void RemoveChild(CrawledTrieNode<T> child)
        {
            _children?.Remove(child);
        }

        public void ReplaceChild(int childIndex, CrawledTrieNode<T> child)
        {
            Debug.Assert(_children is not null);
            _children[childIndex] = child;
        }

        public void AddValue(T value)
        {
            if (_values is null)
                _values = new List<T>();

            _values.Add(value);
        }

        public void RemoveValue(T value)
        {
            _values?.Remove(value);
        }

        public IEnumerable<CrawledTrieNode<T>> DescendantsAndSelf()
        {
            var stack = new Stack<CrawledTrieNode<T>>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                var node = stack.Pop();

                yield return node;

                foreach (var child in node.Children.Reverse())
                    stack.Push(child);
            }
        }

        public IReadOnlyList<CrawledTrieNode<T>> Children => (IReadOnlyList<CrawledTrieNode<T>>)_children ?? Array.Empty<CrawledTrieNode<T>>();

        public IReadOnlyList<T> Values => (IReadOnlyList<T>)_values ?? Array.Empty<T>();
    }
}
