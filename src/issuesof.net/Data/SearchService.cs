using System;
using System.Diagnostics;
using System.Threading.Tasks;

using IssueDb.Crawling;
using IssueDb.Querying;

using Microsoft.ApplicationInsights;

namespace IssuesOfDotNet.Data
{
    public sealed class SearchService : IDisposable
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly IndexService _indexService;

        private CrawledIssueResults _openIssuesResults;

        public SearchService(TelemetryClient telemetryClient, IndexService indexService)
        {
            _telemetryClient = telemetryClient;
            _indexService = indexService;
            _indexService.Changed += IndexService_Changed;
        }

        public void Dispose()
        {
            _indexService.Changed -= IndexService_Changed;
        }

        public CrawledIssueResults Search(string searchText)
        {
            if (_indexService.Index is null)
                return CrawledIssueResults.Empty;

            var isOpenIssuesQuery = searchText == "is:open is:issue";
            if (isOpenIssuesQuery && _openIssuesResults != null)
                return _openIssuesResults;

            var stopwatch = Stopwatch.StartNew();
            var query = IssueQuery.Create(searchText);
            var results = query.Execute(_indexService.Index);
            var elapsed = stopwatch.Elapsed;

            Task.Run(() =>
                _telemetryClient.GetMetric("Search")
                                .TrackValue(elapsed.TotalMilliseconds)
            );

            if (isOpenIssuesQuery && _openIssuesResults is null)
                _openIssuesResults = results;

            return results;
        }

        private void IndexService_Changed(object sender, EventArgs e)
        {
            _openIssuesResults = null;
        }
    }
}
