# Improvements

## General

- Move to `Octokit.WebHookEvents`

## Trie

- Use a mutable trie during construction
- Don't index stop words

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