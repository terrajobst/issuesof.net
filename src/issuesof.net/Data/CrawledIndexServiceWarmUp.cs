using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

namespace IssuesOfDotNet.Data
{
    public sealed class CrawledIndexServiceWarmUp : IHostedService
    {
        public CrawledIndexServiceWarmUp(CrawledIndexService indexService)
        {
            // Just request the type via DI has kicked off initializing the type.
            //
            // We only call this method to avoid the warning about an unused parameter.
            GC.KeepAlive(indexService);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
