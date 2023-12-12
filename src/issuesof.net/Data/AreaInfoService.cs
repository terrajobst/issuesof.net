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

    private void IndexService_Changed(object? sender, EventArgs e)
    {
        var index = _indexService.Index;

        Task.Run(() =>
        {
            if (index is null)
            {
                AreaInfos = ImmutableArray<AreaInfo>.Empty;
                return;
            }

            var entries = index.AreaOwnership.Entries;
            var builder = ImmutableArray.CreateBuilder<AreaInfo>(entries.Count);

            foreach (var entry in entries)
            {
                var areaInfo = new AreaInfo(entry, index);
                builder.Add(areaInfo);
            }

            AreaInfos = builder.MoveToImmutable();
        });
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

