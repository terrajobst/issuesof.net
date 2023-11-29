using System.Diagnostics;

using IssueDb.Querying.Syntax;

using IssuesOfDotNet.Data;

using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;

namespace IssuesOfDotNet.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class CompletionController : Controller
{
    private readonly TelemetryClient _telemetryClient;
    private readonly CompletionService _completionService;

    public CompletionController(TelemetryClient telemetryClient, CompletionService completionService)
    {
        _telemetryClient = telemetryClient;
        _completionService = completionService;
    }

    [HttpGet]
    public CompletionResponse GetCompletions(string q, int pos)
    {
        var stopwatch = Stopwatch.StartNew();
        var syntax = QuerySyntax.Parse(q ?? string.Empty);
        var result = _completionService.Provider.Complete(syntax, pos);
        var response = result is null
            ? null
            : new CompletionResponse
            {
                List = result.Completions.Take(50).ToArray(),
                From = result.Span.Start,
                To = result.Span.End
            };

        var elapsed = stopwatch.Elapsed;

        Task.Run(() =>
            _telemetryClient.GetMetric("Completion")
                            .TrackValue(elapsed.TotalMilliseconds)
        );

        return response;
    }

    public sealed class CompletionResponse
    {
        public string[] List { get; set; }
        public int From { get; set; }
        public int To { get; set; }
    }
}
