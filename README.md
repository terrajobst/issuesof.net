# Issues of .NET

This site shows the issues across all the various GitHub orgs that make up .NET,
such as [aspnet], [dotnet], and [nuget].

[aspnet]: https://github.com/aspnet
[dotnet]: https://github.com/dotnet
[nuget]: https://github.com/nuget

## Next steps

* Find a way to incrementally update the trie so that we can offer quasi
  real-time indexing based on web hook events.

## Features

* We should support all of [GitHub's query syntax](https://docs.github.com/en/github/searching-for-information-on-github/searching-issues-and-pull-requests)
    - It seems some of them use `and` while some use `or` semantics. For
      example, `involves` uses `or` while `label` uses `and`.
    - Looks like we need a text match with an `in` qualifer:
        - `test in:title,body` means "search in title and body"
        - `developer dependency in:title` means "search for developer and
          dependency in title only"
        - Hmm, looks like `in` is just a global modifer, for example `developer
          in:body console in:title` means "search for console and developer in
          either body or title".
    - `in:title`
    - `in:body`
    - `in:comments` 
    - `is:public`
    - `is:internal`
    - `is:private`
    - `mentions:{user}`
    - `team:{orgname}/{teamName}`
    - `commenter:{user}`
    - `involves:{user}`
    - `linked:pr`
    - `linked:issue`
    - `project:{project}`
    - `status:pending`
    - `status:success`
    - `status:failure`
    - `{sha}`
    - `head:{branch}`
    - `base:{branch}`
    - `review:none`
    - `review:required`
    - `review:approved`
    - `review:changes_requested`
    - `reviewed-by:{user}`
    - `review-requested:{user}`
    - `team-review-requested:{user}`
    - `merged:{range}`
    - `no:project`
* We should replace the JavaScript octo-icon library with static CSS-based icons
  because the JavaScript ones don't work well with Blazor (basically, it works
  on initial render but when the page gets updated, for example, due to paging,
  the icons aren't updated while hitting Ctrl+R fixes the problem).

## Onboarding

The tool currently works at the org level and will index all public repos. It
will not, however, index internal or private repos.

In order to add a new org, you need to:

1. Install the [GitHub App](https://github.com/apps/issues-of-net/installations/new)
   into the org
2. Add the org to the [workflow indexing](.github/workflows/schedule.yml#L25)
3. Add the org to the [IsKnownOrg() method](src/issuesof.net/Data/EventService.cs)
