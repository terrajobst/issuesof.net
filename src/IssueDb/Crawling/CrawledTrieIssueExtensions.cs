using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Markdig;
using Snowball;

namespace IssueDb.Crawling;

public static class CrawledTrieIssueExtensions
{
    public static void Add(this CrawledTrie<CrawledIssue> trie, CrawledIssue issue)
    {
        var terms = issue.GetTrieTerms();

        foreach (var term in terms)
            trie.Add(term, issue);
    }

    public static IEnumerable<string> GetTrieTerms(this CrawledIssue issue)
    {
        var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        GetTrieTerms(issue, result);
        return result;
    }

    public static void GetTrieTerms(this CrawledIssue issue, ISet<string> target, bool withBody = false)
    {
        AddTermsFromMarkdown(target, issue.Title);

        // In a perfect world, we'd also index the body. Sadly, this is increases
        // the trie significantly.
        // 
        // On August 22 2022 I did an experiment:
        //
        //      Scenario                | Size
        //      ------------------------|-------
        //      Title                   |  45 MB
        //      Title + Metadata        |  77 MB
        //      Title + Metadata + Body | 665 MB
        //
        //      Note: Here, metadata refers to including details like org:xxx or
        //      label:yyy.
        //
        // Given the size of our App Service (3.5 GB), this is a bit much for just
        // the trie, so for now we're not gonna index the body. For comments it's
        // likely even worse. This is unfortunate, because it would be amazing
        // to use the in-modifier like in:title,body,comment :-(
        //

        if (TryGetPlainText(issue.Body, out var bodyPlainText))
        {
            if (withBody)
                AddTermsFromPlainText(target, bodyPlainText);

            AddMentions(target, bodyPlainText);
        }

        if (issue.IsOpen)
            target.Add("is:open");

        if (issue.IsPullRequest && !issue.IsMerged)
            target.Add("is:unmerged");

        if (issue.IsPullRequest && issue.IsDraft)
            target.Add("is:draft");

        target.Add($"org:{issue.Repo.Org}");
        target.Add($"repo:{issue.Repo.Name}");
        target.Add($"author:{issue.CreatedBy}");

        foreach (var assignee in issue.Assignees)
            target.Add($"assignee:{assignee}");

        foreach (var label in issue.Labels)
            target.Add($"label:{label.Name}");

        foreach (var area in issue.Areas)
            target.Add($"area-under:{area}");

        foreach (var areaNode in issue.AreaNodes)
            target.Add($"area-node:{areaNode}");

        foreach (var areaLead in issue.AreaLeads)
            target.Add($"area-lead:{areaLead}");

        foreach (var areaOwner in issue.AreaOwners)
            target.Add($"area-owner:{areaOwner}");

        foreach (var os in issue.OperatingSystems)
            target.Add($"os:{os}");

        foreach (var osLead in issue.OperatingSystemLeads)
            target.Add($"os-lead:{osLead}");

        foreach (var osOwner in issue.OperatingSystemOwners)
            target.Add($"os-owner:{osOwner}");

        foreach (var arch in issue.Architectures)
            target.Add($"arch:{arch}");

        foreach (var archLead in issue.ArchitectureLeads)
            target.Add($"arch-lead:{archLead}");

        foreach (var archOwner in issue.ArchitectureOwners)
            target.Add($"arch-owner:{archOwner}");

        foreach (var lead in issue.Leads)
            target.Add($"lead:{lead}");

        foreach (var owner in issue.Owners)
            target.Add($"owner:{owner}");

        if (issue.Milestone is not null)
            target.Add($"milestone:{issue.Milestone.Title}");

        AddInvolves(target);
    }

    public static IEnumerable<string> GetTrieTerms(this CrawledIssueComment comment)
    {
        var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        GetTrieTerms(comment, result);
        return result;
    }

    public static void GetTrieTerms(this CrawledIssueComment comment, ISet<string> target, bool withBody = false)
    {
        if (TryGetPlainText(comment.Body, out var plainText))
        {
            AddMentions(target, plainText);

            if (withBody)
                AddTermsFromPlainText(target, plainText);
        }

        target.Add($"commenter:{comment.CreatedBy}");

        AddInvolves(target);
    }

    private static void AddTermsFromMarkdown(ISet<string> target, string markdown)
    {
        if (TryGetPlainText(markdown, out var plainText))
            AddTermsFromPlainText(target, plainText);
    }

    private static bool TryGetPlainText(string markdown, [MaybeNullWhen(false)] out string plainText)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            plainText = string.Empty;
            return true;
        }

        try
        {
            plainText = Markdown.ToPlainText(markdown);
            return true;
        }
        catch (Exception)
        {
            // If we can't convert the Markdown (e.g. very large table or something)
            // we just give up.
            plainText = default;
            return false;
        }        
    }

    private static void AddTermsFromPlainText(ISet<string> target, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var tokens = TextTokenizer.Tokenize(text);

        foreach (var token in tokens)
        {
            var stemmed = _stemmer.Stem(token);
            target.Add(stemmed);
        }
    }

    private static void AddMentions(ISet<string> target, string text)
    {
        foreach (Match match in Regex.Matches(text, @"@(?<UserOrTeam>[a-zA-Z0-9-]+(/[a-zA-Z0-9-]+)?)"))
        {
            var userOrTeam = match.Groups["UserOrTeam"].Value;
            var isTeam = userOrTeam.Contains('/');

            if (isTeam)
                target.Add($"team:{userOrTeam}");
            else
                target.Add($"mentions:{userOrTeam}");
        }
    }

    private static void AddInvolves(ISet<string> target)
    {
        foreach (var entry in target.ToArray())
        {
            var colon = entry.IndexOf(':');
            if (colon < 0)
                continue;

            var key = entry.Substring(0, colon);

            if (key is "author"
                    or "assignee"
                    or "mentions"
                    or "commenter")
            {
                var value = entry.Substring(colon + 1);
                target.Add($"involves:{value}");
            }
        }
    }

    private static readonly EnglishStemmer _stemmer = new EnglishStemmer();
}
