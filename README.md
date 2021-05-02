# Issues of .NET

This site shows the issues across all the various GitHub orgs that make up .NET,
such as [aspnet], [dotnet], and [nuget].

[aspnet]: https://github.com/aspnet
[dotnet]: https://github.com/dotnet
[nuget]: https://github.com/nuget

## Next steps

* Measure how large the issues are vs. how large the trie is
    - Indexing just the title creates working set of ~500 MB, indexing both
      title and body results in 2+GB.
    - We should dump all words contained in the trie and see whether we can
      exclude some during tokenization. I suspect that we have some amount of
      non-words (punctuation).
    - We should also consider using a stop list to avoid indexing super common
      word, such as "the".
* Find a way to incrementally update the trie so that we can offer quasi
  real-time indexing based on web hook events.

## Features

* We should support non-trie lookup query parameters, such as "is:open" or
  "is:pr"
* We should support parentheses and AND/OR/NOT modifiers. We probably want to
  rewrite them into disjunctive normal form.
* We probably want to use CodeMirror's editor for entering the query string so
  that we can offer auto complete, syntax highlighting, and quick info.
* Add a page for filing new issues
    - Should point out how to file issues in VS
    - Navigate to the right repo (e.g. NuGet home vs. gallery)
* We should replace the JavaScript octo-icon library with static CSS-based icons
  because the JavaScript ones don't work well with Blazor (basically, it works
  on initial render but when the page gets updated, for example, due to paging,
  the icons aren't updated while hitting Ctrl+R fixes the problem).
