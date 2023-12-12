using System.Collections.Immutable;
using System.Text.Encodings.Web;
using IssueDb;
using IssueDb.Crawling;
using IssueDb.Querying;

namespace IssuesOfDotNet.Data;

public sealed class AreaInfoService : IDisposable
{
    private readonly IndexService _indexService;

    public AreaInfoService(IndexService indexService)
    {
        _indexService = indexService;
        _indexService.Changed += IndexService_Changed;
    }

    public void Dispose()
    {
        _indexService.Changed -= IndexService_Changed;
    }

    public ImmutableArray<AreaInfo> AreaInfos { get; set; } = ImmutableArray<AreaInfo>.Empty;
    public ImmutableArray<UnmappedAreaInfo> UnmappedAreaInfos { get; set; } = ImmutableArray<UnmappedAreaInfo>.Empty;

    private void IndexService_Changed(object? sender, EventArgs e)
    {
        var index = _indexService.Index;

        Task.Run(() =>
        {
            AreaInfos = ComputeAreaInfos(index);
            UnmappedAreaInfos = ComputeUnmappedAreaInfos(index);
        });
    }

    private static ImmutableArray<AreaInfo> ComputeAreaInfos(CrawledIndex? index)
    {
        if (index is null)
            return ImmutableArray<AreaInfo>.Empty;

        var entries = index.AreaOwnership.Entries;
        var builder = ImmutableArray.CreateBuilder<AreaInfo>(entries.Count);

        foreach (var entry in entries)
        {
            var areaInfo = new AreaInfo(entry, index);
            builder.Add(areaInfo);
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<UnmappedAreaInfo> ComputeUnmappedAreaInfos(CrawledIndex? index)
    {
        if (index is null)
            return ImmutableArray<UnmappedAreaInfo>.Empty;

        var repos = index.Repos;
        var areaOwnership = index.AreaOwnership;

        var reposWithAreaOwnership = areaOwnership.Entries
                                                  .SelectMany(e => e.Definitions)
                                                  .Select(d => $"{d.OrgName}/{d.RepoName}")
                                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var repoByFullName = repos.ToDictionary(r => r.FullName, StringComparer.OrdinalIgnoreCase);
        var unmappedLabels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var repoFullName in reposWithAreaOwnership)
        {
            var repo = repoByFullName[repoFullName];

            foreach (var label in repo.Labels)
            {
                var isAreaLabel = label.Name.StartsWith("area-") ||
                                  label.Name.StartsWith("os-") ||
                                  label.Name.StartsWith("arch-");
                var isMapped = areaOwnership.EntryByLabel.ContainsKey(label.Name);

                if (isAreaLabel && !isMapped)
                {
                    if (!unmappedLabels.TryGetValue(label.Name, out var unmappedRepos))
                    {
                        unmappedRepos = new List<string>();
                        unmappedLabels.Add(label.Name, unmappedRepos);
                    }

                    unmappedRepos.Add(repoFullName);
                }
            }
        }

        return unmappedLabels.Select(kv => new UnmappedAreaInfo(kv.Key, kv.Value, index))
                             .ToImmutableArray();
    }

    public sealed class UnmappedAreaInfo
    {
        public UnmappedAreaInfo(string label, IReadOnlyList<string> repos, CrawledIndex index)
        {
            var searchText = $"is:open label:{label}";
            var query = IssueQuery.Create(searchText);
            var results = query.Execute(index);

            Label = label;
            Repos = repos;
            IssueCount = results.IssueCount;
            Url = "/?q=" + UrlEncoder.Default.Encode(searchText);
        }

        public string Label { get; }
        public IReadOnlyList<string> Repos { get; }
        public int IssueCount { get; }
        public string Url { get; }
    }

    public sealed class AreaInfo
    {
        public AreaInfo(CrawledAreaEntry entry, CrawledIndex index)
        {
            var searchText = $"is:open area:{entry.Area}";
            var query = IssueQuery.Create(searchText);
            var results = query.Execute(index);

            Entry = entry;
            IssueCount = results.IssueCount;
            Url = "/?q=" + UrlEncoder.Default.Encode(searchText);
        }

        public CrawledAreaEntry Entry { get; }
        public int IssueCount { get; }
        public string Url { get; }
    }
}

