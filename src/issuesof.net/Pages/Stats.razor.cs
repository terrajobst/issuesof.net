using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using IssueDb.Crawling;

using IssuesOfDotNet.Data;
using IssuesOfDotNet.net.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages;

public sealed partial class Stats
{
    private Sort _sortBy = Sort.Default;

    [Inject]
    public IndexService TrieService { get; set; }

    [Inject]
    public IJSRuntime JSRuntime { get; set; }

    [Inject]
    public IWebHostEnvironment Environment { get; set; }

    [Inject]
    public NavigationManager NavigationManager { get; set; }

    public int NumberOfTrieNodes { get; set; }
    public int NumberOfTrieStringBytes { get; set; }
    public int NumberOfTrieBytes { get; set; }
    public bool IsDevelopment => Environment.IsDevelopment();

    public Sort SortBy
    {
        get => _sortBy;
        set
        {
            if (_sortBy != value)
            {
                _sortBy = value;
                StateHasChanged();
                ChangeUrl();
            }
        }
    }

    public IEnumerable<RepoStats> Rows => TrieService.IndexStats == null
                                            ? Enumerable.Empty<RepoStats>()
                                            : TrieService.IndexStats.OrderBy(x => x, SortBy.Comparer);

    protected override void OnInitialized()
    {
        if (IsDevelopment && TrieService.Index is not null)
            Calc(TrieService.Index.Trie.Root);

        void Calc(CrawledTrieNode<CrawledIssue> node)
        {
            var nodeBytes = 16;
            var stringBytes = node.Text?.Length * 2 ?? 0;
            var childBytes = node.Children.Length * 8;
            var issueBytes = node.Values.Length * 8;

            NumberOfTrieNodes++;
            NumberOfTrieStringBytes += stringBytes;
            NumberOfTrieBytes += nodeBytes + stringBytes + childBytes + issueBytes;

            foreach (var child in node.Children)
                Calc(child);
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var parameters = QueryHelpers.ParseQuery(uri.Query);

            if (parameters.TryGetValue("sort", out var sortBy))
                SortBy = Sort.Parse(sortBy);
        }
    }

    private async void ChangeUrl()
    {
        var isDefaultQuery = SortBy == Sort.Default;

        if (isDefaultQuery)
            return;

        var query = $"?sort={Uri.EscapeDataString(SortBy.Name)}";

        var uri = new UriBuilder(NavigationManager.Uri)
        {
            Query = query
        }.ToString();

        await JSRuntime.InvokeVoidAsync("Blazor.navigateTo",
                                        uri.ToString(),
                                        /* forceLoad */ false,
                                        /* replace */ false);
    }

    public sealed class Sort
    {
        public static Sort NameAscending { get; } = new Sort("Name", "name-asc", CompareAsc(r => r.FullName));
        public static Sort NameDescending { get; } = new Sort("Name (descending)", "name-desc", CompareDesc(r => r.FullName));

        public static Sort UpdatedAscending { get; } = new Sort("Last Updated", "updated-asc", CompareAsc(r => r.LastUpdatedAt));
        public static Sort UpdatedDescending { get; } = new Sort("Last Updated (descending)", "updated-desc", CompareDesc(r => r.LastUpdatedAt));

        public static Sort OpenCountAscending { get; } = new Sort("#Open", "open-asc", CompareAsc(r => r.NumberOfOpenIssues));
        public static Sort OpenCountDescending { get; } = new Sort("#Open (descending)", "open-desc", CompareDesc(r => r.NumberOfOpenIssues));

        public static Sort TotalCountAscending { get; } = new Sort("#Total", "total-asc", CompareAsc(r => r.NumberOfIssues));
        public static Sort TotalCountDescending { get; } = new Sort("#Total (descending)", "total-desc", CompareDesc(r => r.NumberOfIssues));

        public static Sort SizeAscending { get; } = new Sort("Size", "size-asc", CompareAsc(r => r.Size));
        public static Sort SizeDescending { get; } = new Sort("Size (descending)", "size-desc", CompareDesc(r => r.Size));

        public static Sort Default => NameAscending;

        public static IReadOnlyCollection<Sort> All { get; } = typeof(Sort).GetProperties(BindingFlags.Public | BindingFlags.Static)
                                                                           .Select(p => p.GetValue(null))
                                                                           .OfType<Sort>()
                                                                           .Distinct()
                                                                           .ToArray();

        public static Sort Parse(string text)
        {
            return All.FirstOrDefault(s => string.Equals(s.Name, text, StringComparison.OrdinalIgnoreCase)) ?? NameAscending;
        }

        private static IComparer<RepoStats> CompareAsc<T>(Func<RepoStats, T> selector) => Compare(selector, 1);

        private static IComparer<RepoStats> CompareDesc<T>(Func<RepoStats, T> selector) => Compare(selector, -1);

        private static IComparer<RepoStats> Compare<T>(Func<RepoStats, T> selector, int sign)
        {
            return Comparer<RepoStats>.Create((x, y) => sign * Comparer<T>.Default.Compare(selector(x), selector(y)));
        }

        public Sort(string displayName, string name, IComparer<RepoStats> comparer)
        {
            DisplayName = displayName;
            Name = name;
            Comparer = comparer;
        }

        public string DisplayName { get; }
        public string Name { get; }
        public IComparer<RepoStats> Comparer { get; }
    }
}
