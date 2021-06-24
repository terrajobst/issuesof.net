using IssueDb.Crawling;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Data
{
    public sealed class EventService : GitHubEventProcessor
    {
        private readonly ILogger<EventService> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly GitHubEventProcessingService _processingService;
        private readonly CrawledSubscriptionList _subscriptionList = CrawledSubscriptionList.CreateDefault();

        public EventService(ILogger<EventService> logger, TelemetryClient telemetryClient, GitHubEventProcessingService processingService)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            _processingService = processingService;
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
}
