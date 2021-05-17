using Microsoft.ApplicationInsights;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Data
{
    public sealed class EventService : GitHubEventProcessor
    {
        private readonly TelemetryClient _telemetryClient;

        public EventService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        public override void ProcessMessage(GitHubEventMessage message)
        {
            _telemetryClient.GetMetric("github_" + message.Headers.Event)
                            .TrackValue(1.0);
        }
    }
}
