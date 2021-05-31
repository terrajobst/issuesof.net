using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Data
{
    public sealed partial class GitHubEventProcessingService : IHostedService
    {
        private readonly ILogger<GitHubEventProcessingService> _logger;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<GitHubEventMessage> _messages = new();
        private readonly AutoResetEvent _dataAvailable = new (false);
        private readonly Processor _processor;
        private Task _workerTask;

        public GitHubEventProcessingService(ILogger<GitHubEventProcessingService> logger, IndexService indexService)
        {
            _logger = logger;
            _processor = new Processor(logger, indexService);
        }

        public void Enqueue(GitHubEventMessage message)
        {
            _logger.LogInformation($"Enqueuing message {message}");

            _messages.Enqueue(message);
            _dataAvailable.Set();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _workerTask = Task.Run(() =>
            {
                try
                {
                    Run(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("GitHub event processing was cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in GitHub event processing");
                }
            }, CancellationToken.None);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            return _workerTask;
        }

        private void Run(CancellationToken cancellationToken)
        {
            var waitHandles = new[]
            {
                _dataAvailable,
                cancellationToken.WaitHandle
            };

            while (true)
            {
                WaitHandle.WaitAny(waitHandles);
                cancellationToken.ThrowIfCancellationRequested();

                while (_messages.TryDequeue(out var message))
                {
                    _processor.ProcessMessage(message);
                }
            }
        }
    }
}
