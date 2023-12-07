using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;

namespace IssuesOfDotNet.Shared;

public sealed partial class IndexStatus : IDisposable
{
    [Inject]
    public required IndexService IndexService { get; set; }

    [Inject]
    public required IWebHostEnvironment Environment { get; set; }

    protected override void OnInitialized()
    {
        IndexService.ProgressChanged += IndexService_ProgressChanged;
    }

    public void Dispose()
    {
        IndexService.ProgressChanged -= IndexService_ProgressChanged;
    }

    private void IndexService_ProgressChanged(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }
}
