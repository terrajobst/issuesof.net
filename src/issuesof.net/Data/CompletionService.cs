using System;
using System.Threading.Tasks;

using IssueDb.Crawling;
using IssueDb.Querying.Completion;

using Microsoft.Extensions.Logging;

namespace IssuesOfDotNet.Data
{
    public sealed class CompletionService : IDisposable
    {
        private readonly ILogger<CompletionService> _logger;
        private readonly IndexService _indexService;
        private CrawledIndexCompletionProvider _provider;

        public CompletionService(ILogger<CompletionService> logger,
                                 IndexService indexService)
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
