using System;
using System.Threading.Tasks;

using IssuesOfDotNet.Querying;

namespace IssuesOfDotNet.Data
{
    public sealed class CrawledIndexCompletionService : IDisposable
    {
        private readonly CrawledIndexService _indexService;
        private CrawledIndexCompletionProvider _provider;

        public CrawledIndexCompletionService(CrawledIndexService indexService)
        {
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
                _provider = new CrawledIndexCompletionProvider(_indexService.Index);
            });
        }

        private void IndexService_Changed(object sender, EventArgs e)
        {
            Reload();
        }
    }
}
