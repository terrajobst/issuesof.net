# Improvements

## General

- Move to `Octokit.WebHookEvents`

## Trie

- Use a mutable trie during construction
- Don't index stop words

## Sentiment

- Would be good to identify "hot issues", number of comments, negative
  sentiment, number of negative reactions

## Queries

- Support `,` as a value separator and expand it in the binder, e.g.
  `is:open,issue` should be equivalent with `is:open is:issue`
- Can we do something nice for `or`? It's quite verbose, such as
  `(area:Foo or area:Bar)`
    - `area:in:Foo,Bar`
    - `area-in:Foo,Bar`
    - `area:{Foo,Bar}`
    - `area:[Foo,Bar]`
    - `area:(Foo,Bar)`
    - `area=Foo,Bar`
- Should we support `any:xxx`?
    - It would be expanded as `-no:xxx`

### Mentions and Involves

If we crawl comments we can support these modifiers. All we need to do is store
additional collections at the issue level, such as:

```C#
partial class CrawledIssue
{
  public IReadOnlyList<string> Commenters { get; set; }
  public IReadOnlyList<string> Mentions { get; set; }
  public IReadOnlyList<string> TeamMentions { get; set; }
}
```

We'd just add those as terms to the trie, just like everything else. For
`involves` we can see how expensive it would be to evaluate if we actually
expanded the modifier during binding into the logical `OR`, such as

```
is:open repo:runtime involves:terrajobst
```

would become

```
is:open repo:runtime (author:terrajobst or assignee:terrajobst or mentions:terrajobst or commenter:terrajobst)
```

| Modifier        | Comments                                                                                      |
| --------------- | --------------------------------------------------------------------------------------------- |
| `mentions:xxx`  | The user `xxx` is tagged in description or a comment                                          |
| `team:xxx`      | The team `xxx` is tagged in description or a comment                                          |
| `commenter:xxx` | A comment was authored by `xxx`                                                               |
| `involves:xxx`  | The qualifier is a logical `OR` between the `author`, `assignee`, `mentions`, and `commenter` |
