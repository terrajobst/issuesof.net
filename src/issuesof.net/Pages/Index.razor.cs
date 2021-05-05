using System;
using System.Threading.Tasks;

using IssuesOfDotNet.Data;
using IssuesOfDotNet.Querying;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages
{
    public sealed partial class Index : IDisposable
    {
        private static string _defaultSearch = "is:issue is:open";

        private string _searchText;
        private int _pageNumber;

        [Inject]
        public IJSRuntime JSRuntime { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [Inject]
        public CrawledIndexService TrieService { get; set; }

        [Inject]
        public IWebHostEnvironment Environment { get; set; }

        public bool IsDevelopment => Environment.IsDevelopment();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    Search();
                }
            }
        }

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (_pageNumber != value)
                {
                    _pageNumber = value;
                    UpdateQueryParameters();
                }
            }
        }

        public bool ShowHelp { get; set; }

        public CrawledTrieLookupResult SearchResults { get; private set; } = CrawledTrieLookupResult.Empty;

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
                StateHasChanged();
            });
        }

        private void ApplyQueryParameters()
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var parameters = QueryHelpers.ParseQuery(uri.Query);

            _searchText = null;

            if (parameters.TryGetValue("q", out var q))
                SearchText = q;
            else
                SearchText = _defaultSearch;

            _pageNumber = -1;

            if (parameters.TryGetValue("page", out var pageText) && int.TryParse(pageText, out var page))
                PageNumber = page;
            else
                PageNumber = 0;
        }

        private void Search()
        {
            if (TrieService.Index is null)
                return;

            SearchResults = Find(SearchText);
            PageNumber = 1;
            UpdateQueryParameters();
        }

        [JSInvokable]
        public void Search(string searchText)
        {
            SearchText = searchText;
        }

        private CrawledTrieLookupResult Find(string searchText)
        {
            var query = CrawledIssueQuery.Create(searchText);
            var issues = query.Execute(TrieService.Index);
            return new CrawledTrieLookupResult(issues);
        }

        private async void UpdateQueryParameters()
        {
            var queryString = !string.IsNullOrEmpty(SearchText) && SearchText != _defaultSearch
                ? "?q=" + SearchText
                : string.Empty;

            if (queryString.Length > 0 && PageNumber > 1)
                queryString += "&page=" + PageNumber;

            var uri = new UriBuilder(NavigationManager.Uri)
            {
                Query = queryString
            }.ToString();

            await JSRuntime.InvokeAsync<object>("changeUrl", uri);
            StateHasChanged();
        }
    }
}
