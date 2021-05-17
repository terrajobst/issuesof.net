using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
                _children = children.ToImmutableArray();

            if (values.Any())
                _values = values.ToImmutableArray();
        }

        private ImmutableArray<CrawledTrieNode<T>> _children = ImmutableArray<CrawledTrieNode<T>>.Empty;
        private ImmutableArray<T> _values = ImmutableArray<T>.Empty;

        public string Text { get; set; }

        public void InsertChild(int index, CrawledTrieNode<T> child)
        {
            _children = _children.Insert(index, child);
        }

        public void RemoveChild(CrawledTrieNode<T> child)
        {
            _children = _children.Remove(child);
        }

        public void ReplaceChild(int childIndex, CrawledTrieNode<T> child)
        {
            _children = _children.SetItem(childIndex, child);
        }

        public void AddValue(T value)
        {
            _values = _values.Add(value);
        }

        public void RemoveValue(T value)
        {
            _values = _values.Remove(value);
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

        public ImmutableArray<CrawledTrieNode<T>> Children => _children;

        public ImmutableArray<T> Values => _values;
    }
}
