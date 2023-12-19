using System.Diagnostics.CodeAnalysis;
using System.Text;

public sealed class MutableTrieNode<T>
{
    public MutableTrieNode()
    {        
    }

    public string Text { get; set; } = string.Empty;

    public List<MutableTrieNode<T>>? Children { get; set; }

    public List<T>? Values { get; set; }

    public void InsertChild(int index, MutableTrieNode<T> child)
    {
        Children ??= new List<MutableTrieNode<T>>();
        Children.Insert(index, child);
    }

    public void ReplaceChild(int childIndex, MutableTrieNode<T> child)
    {
        if (Children is not null)
            Children[childIndex] = child;
    }

    public void AddValue(T value)
    {
        Values ??= new List<T>();
        Values.Add(value);
    }

    public void RemoveValue(T value)
    {
        if (Values is not null)
            Values.Remove(value);
    }

    public IEnumerable<MutableTrieNode<T>> DescendantsAndSelf()
    {
        var stack = new Stack<MutableTrieNode<T>>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            yield return node;

            if (node.Children is not null)
            {
                foreach (var child in node.Children.AsEnumerable().Reverse())
                    stack.Push(child);
            }
        }
    }
}

public sealed class MutableTrie<T>
{
    public MutableTrie()
        : this(new MutableTrieNode<T>())
    {
    }

    public MutableTrie(MutableTrieNode<T> root)
    {
        Root = root;
    }

    public IEnumerable<string> GetKeys()
    {
        var sb = new StringBuilder();
        var stack = new Stack<(MutableTrieNode<T> Node, int Offset)>();
        stack.Push((Root, 0));

        while (stack.Count > 0)
        {
            var (node, offset) = stack.Pop();

            sb.Length = offset;
            sb.Append(node.Text);

            if (node.Values?.Count > 0)
                yield return sb.ToString();

            if (node.Children is not null)
            {
                foreach (var child in node.Children.AsEnumerable().Reverse())
                {
                    stack.Push((child, sb.Length));
                }
            }
        }
    }

    public void Add(string text, T value)
    {
        var current = Walk(text, addNodes: true)!;
        current.AddValue(value);
    }

    public void Remove(string text, T value)
    {
        var current = Walk(text, addNodes: false);
        if (current is not null)
            current.RemoveValue(value);
    }

    public List<T>? Lookup(string text)
    {
       var node = Walk(text, addNodes: false);
       if (node is null)
           return null;
    
       return node.Values;
    }

    private MutableTrieNode<T>? Walk(string text, [NotNullWhen(true)] bool addNodes)
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
                current = current.Children![childIndex];
                textStart += current.Text.Length;
                continue;
            }

            if (!addNodes)
                return null;

            childIndex = ~childIndex;

            var previousChild = childIndex == 0
                ? null
                : current.Children![childIndex - 1];

            var commonPrefixLength = GetCommonPrefix(previousChild?.Text, remaining);
            if (commonPrefixLength > 0)
            {
                var prefixChild = new MutableTrieNode<T>()
                {
                    Text = previousChild!.Text.Substring(0, commonPrefixLength)
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
                var nextChild = childIndex == (current.Children?.Count ?? 0)
                    ? null
                    : current.Children![childIndex];

                commonPrefixLength = GetCommonPrefix(nextChild?.Text, remaining);

                if (commonPrefixLength > 0)
                {
                    var prefixChild = new MutableTrieNode<T>()
                    {
                        Text = nextChild!.Text.Substring(0, commonPrefixLength)
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
                    var newChild = new MutableTrieNode<T>
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

    private static int GetCommonPrefix(string? text1, string? text2)
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

    private static int BinarySearch(MutableTrieNode<T> node, string text)
    {
        if (node.Children is null)
            return ~0;

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

    public MutableTrieNode<T> Root { get; }
}