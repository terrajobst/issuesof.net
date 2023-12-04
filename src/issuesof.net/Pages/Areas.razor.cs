using IssueDb;

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

    private AreaOwnership AreaOwnership => IndexService.Index?.AreaOwnership ?? AreaOwnership.Empty;

    private IEnumerable<AreaEntry> Entries => AreaOwnership.Entries.Where(Matches);

    private bool Matches(AreaEntry row)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        return row.Area.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
               row.Leads.Any(l => l.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase)) ||
               row.Owners.Any(o => o.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnInitialized()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var parameters = QueryHelpers.ParseQuery(uri.Query);

        if (parameters.TryGetValue("q", out var filter))
            _filter = filter;
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
}
