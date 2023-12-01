using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace IssuesOfDotNet.Pages;

// Notes:
//
// 1. We should probably pre-load all area teams and their members based on org-teams
// 2. We should do this for all orgs and merge the results
// 3. Maintainers and regular members should be considered owners only (i.e. no leads)
// 4. For a given mapping (area, lead/owner, user) we should remember where it came from
//    (org / team, org / repo / file / line)
// 5. The index should store the areas next to repos
// 6. When tagging issues, we should use the merged structure
// 7. Stored index loader should handle old and new
// 8. Area page should expose areas, leads, and owners. Tooltip should show mapping source.

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

    private IReadOnlyList<AreaInfoRow> AllRows { get; set; } = Array.Empty<AreaInfoRow>();

    private IEnumerable<AreaInfoRow> Rows => AllRows.Where(Matches);

    private sealed class AreaInfoRow
    {
        public string Area { get; init; }
        public IReadOnlyCollection<string> Leads { get; init; }
        public IReadOnlyCollection<string> Owners { get; init; }
    }

    private bool Matches(AreaInfoRow row)
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return true;

        return row.Area.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
               row.Leads.Any(l => l.Contains(Filter, StringComparison.OrdinalIgnoreCase)) ||
               row.Owners.Any(o => o.Contains(Filter, StringComparison.OrdinalIgnoreCase));
    }

    protected override void OnInitialized()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var parameters = QueryHelpers.ParseQuery(uri.Query);

        if (parameters.TryGetValue("q", out var filter))
            _filter = filter;

        if (IndexService.Index is null)
            return;

        var areaDataByArea = new Dictionary<string, (SortedSet<string> Leads, SortedSet<string> Owners)>();

        foreach (var repo in IndexService.Index.Repos)
        {
            if (repo.AreaOwners is not null)
            {
                foreach (var (area, entry) in repo.AreaOwners)
                {
                    if (!areaDataByArea.TryGetValue(area, out var data))
                    {
                        data = (new SortedSet<string>(StringComparer.OrdinalIgnoreCase), new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                        areaDataByArea.Add(area, data);
                    }

                    data.Leads.UnionWith(entry.Leads);
                    data.Owners.UnionWith(entry.Owners);
                }
            }
        }

        var rows = new List<AreaInfoRow>();
        foreach (var (area, (leads, owners)) in areaDataByArea.OrderBy(kv => kv.Key))
        {
            var row = new AreaInfoRow
            {
                Area = area,
                Leads = leads.ToArray(),
                Owners = owners.ToArray()
            };
            rows.Add(row);
        }

        AllRows = rows;
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
