using System;

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

        public EventService(ILogger<EventService> logger, TelemetryClient telemetryClient, GitHubEventProcessingService processingService)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            _processingService = processingService;
        }

        public override void ProcessMessage(GitHubEventMessage message)
        {
            // We're only answering to installations in orgs we care about.
            if (message.Body.Organization is null || !IsKnownOrg(message.Body.Organization.Login))
            {
                _logger.LogWarning($"Rejected message for org '{message.Body.Organization?.Login}'", message);
                return;
            }

            _telemetryClient.GetMetric("github_" + message.Headers.Event)
                            .TrackValue(1.0);

            _processingService.Enqueue(message);
        }

        private static bool IsKnownOrg(string orgName)
        {
            return string.Equals(orgName, "aspnet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(orgName, "dotnet", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(orgName, "nuget", StringComparison.OrdinalIgnoreCase);
        }
    }
}
