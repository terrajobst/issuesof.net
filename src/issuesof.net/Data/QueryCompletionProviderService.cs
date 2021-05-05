using System;
using System.Threading.Tasks;

using IssuesOfDotNet.Querying;

using Microsoft.Extensions.Logging;

namespace IssuesOfDotNet.Data
{
    public sealed class QueryCompletionProviderService : IDisposable
    {
        private readonly ILogger<QueryCompletionProviderService> _logger;
        private readonly CrawledIndexService _indexService;
        private CrawledIndexCompletionProvider _provider;

        public QueryCompletionProviderService(ILogger<QueryCompletionProviderService> logger,
                                              CrawledIndexService indexService)
        {
            _logger = logger;
            _indexService = indexService;
            _indexService.Changed += IndexService_Changed;
        }

        public QueryCompletionProvider Provider => _provider ?? QueryCompletionProvider.Empty;

        public void Dispose()
        {
            _indexService.Changed -= IndexService_Changed;
        }

        private void Reload()
        {
            Task.Run(() =>
            {
                try
                {
                    if (_indexService.Index is not null)
                        _provider = new CrawledIndexCompletionProvider(_indexService.Index);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Can't create CrawledIndexCompletionProvider");
                }
            });
        }

        private void IndexService_Changed(object sender, EventArgs e)
        {
            Reload();
        }
    }
}
