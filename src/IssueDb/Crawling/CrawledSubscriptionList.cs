using System.Text.Json;

namespace IssueDb.Crawling;

public sealed class CrawledSubscriptionList
{
    public static CrawledSubscriptionList CreateDefault()
    {
        var result = new CrawledSubscriptionList();
        foreach (var entry in SubscriptionEntry.Load())
        {
            if (entry.Repos is null)
            {
                result.Add(entry.Org);
            }
            else
            {
                foreach (var repo in entry.Repos)
                    result.Add(entry.Org, repo);
            }
        }

        return result;
    }

    private readonly Dictionary<string, SortedSet<string>> _orgRepos = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

    public void Add(string orgAndRepo)
    {
        var (org, repo) = ParseOrgAndRepo(orgAndRepo);
        Add(org, repo);
    }

    public void Add(string org, string? repo)
    {
        if (!_orgRepos.TryGetValue(org, out var repos))
        {
            repos = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            _orgRepos.Add(org, repos);
        }

        if (repo is not null)
            repos.Add(repo);
    }

    public bool Contains(string orgAndRepo)
    {
        var (org, repo) = ParseOrgAndRepo(orgAndRepo);
        if (repo is null)
            return _orgRepos.ContainsKey(org);

        return Contains(org, repo);
    }

    public bool Contains(string org, string repo)
    {
        return _orgRepos.TryGetValue(org, out var repos) && (repos.Count == 0 || repos.Contains(repo));
    }

    public IEnumerable<string> Orgs => _orgRepos.Keys;

    public IReadOnlySet<string>? GetRepos(string org)
    {
        if (_orgRepos.TryGetValue(org, out var repos))
            return repos;

        return null;
    }

    private static (string Org, string? Repo) ParseOrgAndRepo(string text)
    {
        var indexOfSlash = text.IndexOf("/");
        if (indexOfSlash < 0)
        {
            return (text, null);
        }
        else
        {
            var org = text.Substring(0, indexOfSlash);
            var repo = text.Substring(indexOfSlash + 1);
            return (org, repo);
        }
    }

    private sealed class SubscriptionEntry
    {
        public SubscriptionEntry(string org, IReadOnlyList<string> repos)
        {
            Org = org;
            Repos = repos;
        }

        public string Org { get; }
        public IReadOnlyList<string> Repos { get; }

        public static IReadOnlyList<SubscriptionEntry> Load()
        {
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            byte[] bytes;

            using (var memoryStream = new MemoryStream())
            using (var stream = typeof(SubscriptionEntry).Assembly.GetManifestResourceStream("IssueDb.subscriptions.json"))
            {
                stream!.CopyTo(memoryStream);
                bytes = memoryStream.ToArray();
            }

            return JsonSerializer.Deserialize<IReadOnlyList<SubscriptionEntry>>(bytes, options)!;
        }
    }
}
