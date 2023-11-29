using Markdig;

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
        AddTermsFromMarkdown(result, issue.Title);

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
        // AddTermsFromMarkdown(result, issue.Body);

        result.Add($"org:{issue.Repo.Org}");
        result.Add($"repo:{issue.Repo.Name}");
        result.Add($"repo:{issue.Repo.FullName}");
        result.Add($"author:{issue.CreatedBy}");

        foreach (var assignee in issue.Assignees)
            result.Add($"assignee:{assignee}");

        foreach (var label in issue.Labels)
            result.Add($"label:{label.Name}");

        foreach (var area in issue.Areas)
            result.Add($"area-under:{area}");

        foreach (var areaNode in issue.AreaNodes)
            result.Add($"area-node:{areaNode}");

        foreach (var areaLead in issue.AreaLeads)
            result.Add($"area-lead:{areaLead}");

        foreach (var areaPod in issue.AreaPods)
            result.Add($"area-pod:{areaPod}");

        foreach (var areaOwner in issue.AreaOwners)
            result.Add($"area-owner:{areaOwner}");

        if (issue.Milestone is not null)
            result.Add($"milestone:{issue.Milestone.Title}");

        return result;
    }

    private static void AddTermsFromMarkdown(ISet<string> target, string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return;

        try
        {
            var plainText = Markdown.ToPlainText(markdown);
            AddTermsFromPlainText(target, plainText);
        }
        catch (Exception)
        {
            // If we can't convert the Markdown (e.g. very large table or something)
            // we just give up.
            return;
        }
    }

    private static void AddTermsFromPlainText(ISet<string> target, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var tokens = TextTokenizer.Tokenize(text);
        target.UnionWith(tokens);
    }
}
