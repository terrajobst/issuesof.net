using System;
using System.Diagnostics;
using System.Threading.Tasks;

using IssueDb.Crawling;
using IssueDb.Querying;

using IssuesOfDotNet.Data;

using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages
{
    public sealed partial class Index : IDisposable
    {
        private static readonly string _defaultSearch = "is:issue is:open";

        private string _searchText;
        private int _pageNumber;

        [Inject]
        public IJSRuntime JSRuntime { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [Inject]
        public IndexService TrieService { get; set; }

        [Inject]
        public IWebHostEnvironment Environment { get; set; }

        [Inject]
        public TelemetryClient TelemetryClient { get; set; }

        public bool IsDevelopment => Environment.IsDevelopment();

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (_pageNumber != value)
                {
                    _pageNumber = value;
                    ChangeUrl();
                }
            }
        }

        public CrawledIssueResults SearchResults { get; private set; } = CrawledIssueResults.Empty;

        protected override void OnInitialized()
        {
            TrieService.Changed += TrieService_Changed;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var dotNetObjRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("registerPage", dotNetObjRef);
                ApplyQueryParameters();
            }
        }

        public void Dispose()
        {
            TrieService.Changed -= TrieService_Changed;
        }

        private void TrieService_Changed(object sender, EventArgs e)
        {
            InvokeAsync(() =>
            {
                ApplyQueryParameters();
            });
        }

        private void ApplyQueryParameters()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var parameters = QueryHelpers.ParseQuery(uri.Query);

            if (parameters.TryGetValue("q", out var q))
                _searchText = q;
            else
                _searchText = _defaultSearch;

            if (parameters.TryGetValue("page", out var pageText) && int.TryParse(pageText, out var page))
                _pageNumber = page;
            else
                _pageNumber = 0;

            SearchResults = Find(_searchText);
            StateHasChanged();
        }

        [JSInvokable]
        public void Search(string searchText)
        {
            SearchResults = Find(searchText);
            PageNumber = 1;
            ChangeUrl();
            StateHasChanged();
        }

        private CrawledIssueResults Find(string searchText)
        {
            _searchText = searchText;

            if (TrieService.Index is null)
                return CrawledIssueResults.Empty;

            var stopwatch = Stopwatch.StartNew();
            var query = IssueQuery.Create(searchText);
            var issues = query.Execute(TrieService.Index);
            var results = new CrawledIssueResults(issues);
            var elapsed = stopwatch.Elapsed;

            TelemetryClient.GetMetric("Search")
                           .TrackValue(elapsed.TotalMilliseconds);

            return results;
        }

        private async void ChangeUrl()
        {
            var isDefaultQuery = (string.IsNullOrEmpty(_searchText) ||
                                  _searchText.Trim() == _defaultSearch) &&
                                  PageNumber <= 1;

            if (isDefaultQuery)
                return;

            var query = $"?q={Uri.EscapeDataString(_searchText)}";

            if (query.Length > 0 && PageNumber > 1)
                query += $"&page={PageNumber}";

            var uri = new UriBuilder(NavigationManager.Uri)
            {
                Query = query
            }.ToString();

            // Let's update the URL on the client without navigating.
            //
            // NOTE: We want to replace the history state because this is done on every
            //       keystroke in the search box.

            await JSRuntime.InvokeVoidAsync("Blazor.navigateTo",
                                            uri.ToString(),
                                            /* forceLoad */ false,
                                            /* replace */ true);
        }
    }
}
