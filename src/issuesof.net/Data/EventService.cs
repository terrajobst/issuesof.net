using IssueDb.Crawling;
using IssueDb.Eventing;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Primitives;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Data;

public sealed class EventService : GitHubEventProcessor
{
    private readonly ILogger<EventService> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly GitHubEventProcessingService _processingService;
    private readonly CrawledSubscriptionList _subscriptionList = CrawledSubscriptionList.CreateDefault();
    private readonly GitHubEventStore _store;

    public EventService(ILogger<EventService> logger,
                        TelemetryClient telemetryClient,
                        GitHubEventProcessingService processingService,
                        IConfiguration configuration)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        _processingService = processingService;
        _store = new GitHubEventStore(configuration["AzureStorageConnectionString"]!);

        // TODO: Hack, this should live somewhere else
        // LoadEventsAsync().Wait();
    }

    private async Task LoadEventsAsync()
    {
        try
        {
            foreach (var name in await _store.ListAsync())
            {
                var payload = await _store.LoadAsync(name);
                var headers = payload.Headers.ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value.ToArray()));
                var body = payload.Body;
                var message = GitHubEventMessage.Parse(headers, body);
                _processingService.Enqueue(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't load stored events");
        }
    }

    public override void Process(IDictionary<string, StringValues> headers, string body)
    {
        try
        {
            var message = GitHubEventMessage.Parse(headers, body);
            var delivery = message?.Headers?.Delivery;
            var orgName = message?.Body?.Organization?.Login;
            var repoName = message?.Body?.Repository?.Name;
            var timestamp = DateTime.UtcNow;

            if (delivery is not null &&
                orgName is not null &&
                repoName is not null)
            {
                var payload = new GitHubEventPayload(headers.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToArray()!), body);
                var name = new GitHubEventPayloadName(orgName, repoName, timestamp, delivery);
                _logger.LogInformation($"Storing event {name}");
                _store.SaveAsync(name, payload).Wait();
            }
            else
            {
                _logger.LogWarning("Incomplete event {orgName}/{repoName}: {delivery}", orgName, repoName, delivery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't handle event");
        }

        base.Process(headers, body);
    }

    public override void ProcessMessage(GitHubEventMessage message)
    {
        var orgName = message.Body.Organization?.Login;
        var repoName = message.Body.Repository?.Name;

        // We're only answering to installations in orgs and repos we care about.

        if (orgName is null || !_subscriptionList.Contains(orgName))
        {
            _logger.LogWarning($"Rejected message for org '{orgName}'", message);
            return;
        }

        if (repoName is not null && !_subscriptionList.Contains(orgName, repoName))
        {
            _logger.LogWarning($"Rejected message for repo '{orgName}/{repoName}'", message);
            return;
        }

        _telemetryClient.GetMetric("github_" + message.Headers.Event)
                        .TrackValue(1.0);

        _processingService.Enqueue(message);
    }
}
