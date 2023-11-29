using System.Text;

namespace IssueDb.Crawling;

// TODO: We should compactify this data structure this further (see perf below)
//
// Performance
// ===========
//
// Replacing the implementation of our trie with different data structures yields
// these results:
//
//      # | Approach   |  Issues           | Null Issues
//      ---------------|-------------------|-----------------
//      1 | UTF8 Trie  | 249,796,264 bytes | 74,505,928 bytes
//      2 | Trie       | 253,479,976 bytes | 78,213,392 bytes
//      3 | Dictionary | 266,435,040 bytes | 91,148,128 bytes
//
// Using the UTF8 trie and then storing the issues as a byte array of 7-bit compressed
// ints and then compressing them using different algorithms yields these results:
//
//      # | Algorithm  | Issues            | Null Issues
//      --|------------|-------------------|-----------------
//      1 | Deflate    | 224,990,336 bytes | 49,699,984 bytes
//      2 | Brotli     | 225,473,400 bytes | 50,183,024 bytes
//      3 | None       | 226,650,352 bytes | 51,360,000 bytes
//      4 | zlib       | 227,115,648 bytes | 51,825,272 bytes
//      5 | gzip       | 229,847,168 bytes | 54,556,792 bytes

public sealed class CrawledTrie<T>
{
    public CrawledTrie()
        : this(new CrawledTrieNode<T>())
    {
    }

    public CrawledTrie(CrawledTrieNode<T> root)
    {
        Root = root;
    }

    public IEnumerable<string> GetKeys()
    {
        var sb = new StringBuilder();
        var stack = new Stack<(CrawledTrieNode<T> Node, int Offset)>();
        stack.Push((Root, 0));

        while (stack.Count > 0)
        {
            var (node, offset) = stack.Pop();

            sb.Length = offset;
            sb.Append(node.Text);

            if (node.Values.Any())
                yield return sb.ToString();

            foreach (var child in node.Children.Reverse())
            {
                stack.Push((child, sb.Length));
            }
        }
    }

    public void Add(string text, T value)
    {
        var current = Walk(text, addNodes: true);
        current.AddValue(value);
    }

    public void Remove(string text, T value)
    {
        var current = Walk(text, addNodes: false);
        if (current is not null)
            current.RemoveValue(value);
    }

    public IEnumerable<T> Lookup(string text)
    {
        var node = Walk(text, addNodes: false);
        if (node is null)
            return Enumerable.Empty<T>();

        return node.Values;
    }

    private CrawledTrieNode<T> Walk(string text, bool addNodes)
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
                var prefixChild = new CrawledTrieNode<T>()
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
                var nextChild = childIndex == current.Children.Length
                    ? null
                    : current.Children[childIndex];

                commonPrefixLength = GetCommonPrefix(nextChild?.Text, remaining);

                if (commonPrefixLength > 0)
                {
                    var prefixChild = new CrawledTrieNode<T>()
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
                    var newChild = new CrawledTrieNode<T>
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

    private static int BinarySearch(CrawledTrieNode<T> node, string text)
    {
        var lo = 0;
        var hi = node.Children.Length - 1;

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

    public CrawledTrieNode<T> Root { get; }
}
