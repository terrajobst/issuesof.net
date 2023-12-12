# Trie

This document describes the trie we're using to index the issues.

## General implementation

The general implementation can be found in [CrawledTrie.cs]. The general
structure is:

```C#
public class CrawledTrie<T>
{
    public CrawledTrieNode<T> Root { get; }

    public void Add(string text, T value);
    public void Remove(string text, T value);
    public IEnumerable<T> Lookup(string text);
}

public class CrawledTrieNode<T>
{
    public string Text { get; }
    public IReadOnlyList<CrawledTrieNode<T>> Children { get; }
    public IReadOnlyList<T> Values { get; }
}
```

In our case the `T` is a `CrawledIssue`. When indexing an issue, we only
consider it's title and a set of tagged metadata, such as which repo, assignees
and labels.

[CrawledTrie.cs]: ../src/IssueDb/Crawling/CrawledTrie.cs

## Distribution of child counts

How big or wide is our trie? To answer this, let's first look at how many
children nodes typically have. We have a decent number of nodes with zero
children (leaves), which makes up about two third of the entire trie. The root
nodes is the biggest with ~340 children.

| Child Counts    | #Nodes  |
| --------------- | ------- |
| 0               | 209,122 |
| 1-10            | 98,628  |
| 11-20           | 2,578   |
| 21-30           | 585     |
| 31-40           | 75      |
| 41-50           | 1       |
| 341-350         | 1       |
| **Grand Total** | 310,990 |

## Distribution of text lengths

Then, let's look at the length of the text per node. There is one node with an
empty text (the root). About a third of the nodes has a one-character string,
almost the entire rest has a length of 2-10 with the remaining 10% being in the
range of 10-300.

| Text Length     | #Nodes  |
| --------------- | ------- |
| 0               | 1       |
| 1               | 98,719  |
| 2               | 32,589  |
| 3               | 30,697  |
| 4               | 30,238  |
| 5               | 24,397  |
| 6               | 20,848  |
| 7               | 16,600  |
| 8               | 12,560  |
| 9               | 10,342  |
| 10              | 6,811   |
| 11-20           | 22,244  |
| 21-30           | 3,684   |
| 31-40           | 984     |
| 41-50           | 181     |
| 51-60           | 62      |
| 61-70           | 19      |
| 71-80           | 10      |
| 81-90           | 2       |
| 91-100          | 1       |
| 291-300         | 1       |
| **Grand Total** | 310,990 |

## Distribution of text duplications

Speaking of text, let's also consider how many of our texts are duplicated. A
decent number only occurs once, but some strings occur a lot; hence string
interning is definitely worth it.

| Duplication Counts | #Nodes  |
| ------------------ | ------- |
| <2                 | 85,879  |
| 2-11               | 14,126  |
| 12-21              | 741     |
| 22-31              | 333     |
| 32-41              | 192     |
| 42-51              | 144     |
| 52-61              | 98      |
| 62-71              | 56      |
| 72-81              | 40      |
| 82-91              | 24      |
| 92-101             | 22      |
| 102-111            | 16      |
| 112-121            | 20      |
| 122-131            | 12      |
| 132-141            | 7       |
| 142-151            | 5       |
| 152-161            | 3       |
| 162-171            | 3       |
| 172-181            | 6       |
| 182-191            | 3       |
| 192-201            | 2       |
| 202-211            | 5       |
| 212-221            | 5       |
| 222-231            | 5       |
| 232-241            | 3       |
| 242-251            | 3       |
| 252-261            | 2       |
| 262-271            | 3       |
| 272-281            | 1       |
| 282-291            | 2       |
| 312-321            | 2       |
| 322-331            | 3       |
| 332-341            | 1       |
| 342-351            | 1       |
| 352-361            | 1       |
| 362-371            | 2       |
| 402-411            | 1       |
| 412-421            | 1       |
| 432-441            | 1       |
| 442-451            | 1       |
| 472-481            | 1       |
| 492-501            | 2       |
| 522-531            | 1       |
| 572-581            | 1       |
| 592-601            | 1       |
| 632-641            | 2       |
| 902-911            | 1       |
| 912-921            | 1       |
| 932-941            | 1       |
| 962-971            | 1       |
| 1042-1051          | 1       |
| 1082-1091          | 1       |
| 1312-1321          | 1       |
| 1422-1431          | 1       |
| 1592-1601          | 1       |
| 1672-1681          | 1       |
| 1712-1721          | 1       |
| 1772-1781          | 2       |
| 1822-1831          | 1       |
| 2002-2011          | 1       |
| 2132-2141          | 1       |
| 2202-2211          | 1       |
| 2252-2261          | 1       |
| 2312-2321          | 1       |
| 2332-2341          | 1       |
| 2482-2491          | 1       |
| 2502-2511          | 1       |
| 2562-2571          | 1       |
| 2742-2751          | 1       |
| 2922-2931          | 1       |
| 3012-3021          | 1       |
| 3082-3091          | 1       |
| 3522-3531          | 1       |
| 3612-3621          | 1       |
| 3702-3711          | 1       |
| 3972-3981          | 1       |
| 4112-4121          | 1       |
| 4392-4401          | 1       |
| 4422-4431          | 1       |
| 4542-4551          | 1       |
| 5472-5481          | 1       |
| 5652-5661          | 1       |
| 6842-6851          | 1       |
| **Grand Total**    | 101,821 |

## Size of the trie

With string de-duplication the trie is around 70 MB. Most of the size comes from
the leaves, that is the collections that contain the values.

| Area            | Size (bytes) | Megabytes (MB) |
| --------------- | ------------ | -------------- |
| Nodes           | 4,975,840    | 4.7            |
| Text            | 2,566,696    | 2.4            |
| Children        | 1,651,428    | 1.6            |
| Values          | 62,689,624   | 59.8           |
| **Grand Total** | 71,883,588   | 68.6           |

## Distribution of tagged keys

We store common metadata about issues in the trie as well. We call those tagged
keys because they are just strings of the form `tag:value`.

For example, when an issue has two labels X and Y, we add two additional terms
to the trie, `label:X` and `label:Y`. We do similar things for all metadata that
is string-based. We don't, however, store booleans or range-based values (e.g.
`is:open`, `is:issue`, `created`, `updated` etc).

If we look at the tags of this metadata and count how many issues they are
associated with we can see half all trie issue associations are actually
metadata based (in this table a blank tag indicates a trie term that doesn't
contain a `:` and therefore isn't tagged metadata).

| Tag             | #Values    |
| --------------- | ---------- |
|                 | 7,636,726  |
| repo            | 1,889,068  |
| label           | 1,182,730  |
| org             | 944,534    |
| author          | 944,534    |
| area-owner      | 575,499    |
| owner           | 562,783    |
| area-under      | 389,234    |
| area-node       | 388,951    |
| milestone       | 276,915    |
| assignee        | 227,288    |
| area-lead       | 162,565    |
| lead            | 158,828    |
| arch            | 36,703     |
| os              | 33,263     |
| arch-owner      | 12,674     |
| arch-lead       | 6,337      |
| os-owner        | 2,014      |
| os-lead         | 1,301      |
| **Grand Total** | 15,431,947 |

## Stemming

It's common practice for tries to use stemming to reduce the input space, for
example, by replacing `forms` with `form` and `acceptance` with `accept`.

I've tried this with a C# implementation and the reduction was minimal. The trie
has a total of a quarter million keys, out of which only a 151k are words, the
other ones are tagged metadata which we shouldn't stem.

And out of the 151k words stemming only reduces it to 142k.

| Kind  | Count   |
| ----- | ------- |
| Keys  | 243,198 |
| Words | 151,672 |
| Stems | 142,170 |

## Trie terms vs predicates

When searching for issues we use both trie lookups as well as simple predicates
(i.e. a sequential scan through all issues).

In some cases, a predicate is necessary, for example for range-based lookups
such as `comments:>=100`. In other cases we could either store a term in the
trie or use a predicate such as `is:open` or `is:issue`.

Looking up issues via a term in the trie is really beneficial if it only applies
to small number of issues.

If it applies to a large number, it becomes less ideal because it will bloat the
trie and in the end we still have process a large number of issues. If we define
"small number of issues" as less than 20%, then these terms would be very
useful:

| Term        | Selectivity |
| ----------- | ----------- |
| is:draft    | 0.7%        |
| is:unmerged | 6.8%        |
| is:open     | 8.3%        |
| is:archived | 14.6%       |

> [!NOTE]
>
> `is:archived` isn't used because it's not a function of the issue itself but
> of the repo. When an repo archival status changes, we'd have to potentially
> update many issues. Since querying for archival state is practically rare, a
> predicate scan is easier.

These would be less useful:

| Term          | Selectivity |
| ------------- | ----------- |
| is:locked     | 32.5%       |
| is:issue      | 43.7%       |
| no:labels     | 44.0%       |
| is:merged     | 49.5%       |
| is:pr         | 56.3%       |
| is:unlocked   | 67.5%       |
| no:milestone  | 70.7%       |
| no:area       | 73.0%       |
| no:assignees  | 77.3%       |
| no:area-lead  | 84.7%       |
| no:lead       | 85.6%       |
| no:area-owner | 85.8%       |
| no:owner      | 86.6%       |
| is:closed     | 91.7%       |
| no:arch       | 96.4%       |
| no:os         | 96.6%       |
| no:arch-lead  | 99.3%       |
| no:arch-owner | 99.3%       |
| no:os-lead    | 99.9%       |
| no:os-owner   | 99.9%       |
