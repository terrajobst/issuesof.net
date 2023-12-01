using System.Collections.Concurrent;

using Octokit.Webhooks;

namespace IssuesOfDotNet.Data;

public sealed partial class GitHubEventProcessingService : BackgroundService
{
    private readonly ConcurrentQueue<(WebhookHeaders Headers, WebhookEvent WebhookEvent)> _messages = new();
    private readonly AutoResetEvent _dataAvailable = new(false);
    private readonly Processor _processor;

    public GitHubEventProcessingService(ILogger<GitHubEventProcessingService> logger, IndexService indexService)
    {
        _processor = new Processor(logger, indexService);
    }

    public void Enqueue(WebhookHeaders headers, WebhookEvent webhookEvent)
    {
        _messages.Enqueue((headers, webhookEvent));
        _dataAvailable.Set();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var waitHandles = new[]
        {
            _dataAvailable,
            stoppingToken.WaitHandle
        };

        while (true)
        {
            WaitHandle.WaitAny(waitHandles);
            stoppingToken.ThrowIfCancellationRequested();

            while (_messages.TryDequeue(out var message))
            {
                await _processor.ProcessWebhookAsync(message.Headers, message.WebhookEvent);
            }
        }
    }
}
