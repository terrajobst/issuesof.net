using IssuesOfDotNet.Data;

using Microsoft.AspNetCore.Components;

namespace IssuesOfDotNet.Shared;

public sealed partial class IndexStatus
{
    [Inject]
    public IndexService IndexService { get; set; }

    [Inject]
    public IWebHostEnvironment Environment { get; set; }
}
