using System.Collections.Frozen;
using System.Text.Encodings.Web;

using IssueDb;
using IssueDb.Crawling;
using IssueDb.Querying;

using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages;

public sealed partial class Areas
{
    private string _filter = string.Empty;

    [Inject]
    public IndexService IndexService { get; set; }

    [Inject]
    public IJSRuntime JSRuntime { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; set; }

    public string Filter
    {
        get => _filter;
        set
        {
            if (_filter != value)
            {
                _filter = value;
                ChangeUrl();
            }
        }
    }

    private CrawledAreaOwnership AreaOwnership { get; set; } = CrawledAreaOwnership.Empty;

    private FrozenDictionary<CrawledAreaEntry, AreaQueryInfo> AreaQueryInfos { get; set; } = FrozenDictionary<CrawledAreaEntry, AreaQueryInfo>.Empty;

    private IEnumerable<CrawledAreaEntry> Entries => AreaOwnership.Entries.Where(Matches);

    private bool Matches(CrawledAreaEntry row)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        return row.Area.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
               row.Leads.Any(l => l.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase)) ||
               row.Owners.Any(o => o.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase)) ||
               row.Definitions.Any(d => $"{d.OrgName}/{d.RepoName}".Contains(Filter, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnInitialized()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var parameters = QueryHelpers.ParseQuery(uri.Query);

        if (parameters.TryGetValue("q", out var filter))
            _filter = filter;

        var index = IndexService.Index;
        if (index is not null)
        {
            AreaOwnership = index.AreaOwnership;
            AreaQueryInfos = AreaOwnership.Entries
                .Select(e => new AreaQueryInfo(e, index))
                .ToFrozenDictionary(e => e.Entry);
        }
    }

    private async void ChangeUrl()
    {
        var query = new Dictionary<string, object>
        {
            ["q"] = string.IsNullOrWhiteSpace(Filter) ? null : Filter
        };

        var uri = NavigationManager.GetUriWithQueryParameters(query);
        await JSRuntime.InvokeVoidAsync("Blazor.navigateTo",
                                        uri.ToString(),
                                        /* forceLoad */ false,
                                        /* replace */ false);
    }

    private sealed class AreaQueryInfo
    {
        public AreaQueryInfo(CrawledAreaEntry entry, CrawledIndex index)
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
