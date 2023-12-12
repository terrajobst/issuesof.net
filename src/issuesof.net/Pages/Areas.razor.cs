using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages;

public sealed partial class Areas
{
    private string? _filter;
    private bool _showUnmapped;

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required AreaInfoService AreaInfoService { get; set; }

    public bool ShowUnmapped
    {
        get => _showUnmapped;
        set
        {
            if (_showUnmapped!= value)
            {
                _showUnmapped = value;
                ChangeUrl();
            }
        }
    }

    public string? Filter
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

    private IEnumerable<AreaInfoService.AreaInfo> Entries => AreaInfoService.AreaInfos.Where(Matches);

    private IEnumerable<AreaInfoService.UnmappedAreaInfo> UnmappedEntries => AreaInfoService.UnmappedAreaInfos.Where(Matches);

    private bool Matches(AreaInfoService.AreaInfo info)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        return info.Entry.Label.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
               info.Entry.Area.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
               info.Entry.Leads.Any(l => l.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase)) ||
               info.Entry.Owners.Any(o => o.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase)) ||
               info.Entry.Definitions.Any(d => $"{d.OrgName}/{d.RepoName}".Contains(Filter, StringComparison.OrdinalIgnoreCase));
    }

    private bool Matches(AreaInfoService.UnmappedAreaInfo info)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        return info.Label.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
               info.Repos.Any(r => r.Contains(Filter, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnInitialized()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var parameters = QueryHelpers.ParseQuery(uri.Query);

        if (parameters.TryGetValue("unmapped", out var unmappedText) && bool.TryParse(unmappedText, out var showUnmapped))
            _showUnmapped = showUnmapped;

        if (parameters.TryGetValue("q", out var filter))
            _filter = filter;
    }

    private async void ChangeUrl()
    {
        var query = new Dictionary<string, object?>
        {
            ["unmapped"] = ShowUnmapped ? true : null,
            ["q"] = string.IsNullOrWhiteSpace(Filter) ? null : Filter
        };

        var uri = NavigationManager.GetUriWithQueryParameters(query);
        await JSRuntime.InvokeVoidAsync("Blazor.navigateTo",
                                        uri.ToString(),
                                        /* forceLoad */ false,
                                        /* replace */ false);
    }
}
