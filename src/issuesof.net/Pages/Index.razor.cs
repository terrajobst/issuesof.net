using System;

using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages
{
    public sealed partial class Index : IDisposable
    {
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

        public CrawledTrieLookupResult SearchResults { get; private set; } = CrawledTrieLookupResult.Empty;

        protected override void OnInitialized()
        {
            TrieService.Changed += TrieService_Changed;
            ApplyQueryParameters();
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
                SearchText = string.Empty;

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

            SearchResults = TrieService.Index.Trie.LookupIssues(SearchText);
            PageNumber = 1;
            UpdateQueryParameters();
        }

        private async void UpdateQueryParameters()
        {
            var queryString = string.IsNullOrEmpty(SearchText)
                ? string.Empty
                : "?q=" + SearchText;

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
