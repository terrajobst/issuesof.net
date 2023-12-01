using IssueDb.Crawling;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Primitives;

using Octokit.Webhooks;

namespace IssuesOfDotNet.Data;

public sealed class EventService : WebhookEventProcessor
{
    private readonly ILogger<EventService> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly GitHubEventProcessingService _processingService;
    private readonly CrawledSubscriptionList _subscriptionList = CrawledSubscriptionList.CreateDefault();

    public EventService(ILogger<EventService> logger,
                        TelemetryClient telemetryClient,
                        GitHubEventProcessingService processingService)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        _processingService = processingService;
    }

    public override Task ProcessWebhookAsync(IDictionary<string, StringValues> headers, string body)
    {
        return base.ProcessWebhookAsync(headers, body);
    }

    public override Task ProcessWebhookAsync(WebhookHeaders headers, WebhookEvent webhookEvent)
    {
        var orgName = webhookEvent.Organization?.Login;
        var repoName = webhookEvent.Repository?.Name;

        // We're only answering to installations in orgs and repos we care about.

        if (orgName is null || !_subscriptionList.Contains(orgName))
        {
            _logger.LogWarning($"Rejected event for org '{orgName}'", webhookEvent);
            return Task.CompletedTask;
        }

        if (repoName is not null && !_subscriptionList.Contains(orgName, repoName))
        {
            _logger.LogWarning($"Rejected event for repo '{orgName}/{repoName}'", webhookEvent);
            return Task.CompletedTask;
        }

        _telemetryClient.GetMetric("github_" + headers.Event)
                        .TrackValue(1.0);

        _processingService.Enqueue(headers, webhookEvent);
        return Task.CompletedTask;
    }
}
