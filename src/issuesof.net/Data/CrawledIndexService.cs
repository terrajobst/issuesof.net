using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Azure.Storage.Blobs;

using IssuesOfDotNet.net.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IssuesOfDotNet.Data
{
    public sealed class CrawledIndexService
    {
        private readonly ILogger<CrawledIndexService> _logger;
        private readonly IConfiguration _configuration;
        private string _progress;

        public CrawledIndexService(ILogger<CrawledIndexService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            Reload();
        }

        public void Reload()
        {
            Exeption = null;
            Index = null;
            IndexStats = Array.Empty<RepoStats>();

            Task.Run(async () =>
            {
                try
                {
                    var azureConnectionString = _configuration["AzureStorageConnectionString"];
                    var binDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
                    var indexName = "index.cicache";
                    var indexFile = Path.Combine(binDirectory, indexName);

                    if (!File.Exists(indexFile))
                    {
                        ProgressText = $"Downloading index...";
                        var blobClient = new BlobClient(azureConnectionString, "index", indexName);
                        await blobClient.DownloadToAsync(indexFile);
                    }

                    ProgressText = "Loading index...";
                    Index = await CrawledIndex.LoadAsync(indexFile);
                    Exeption = null;
                    IndexStats = CreateIndexStats(Index);
                    ProgressText = null;
                }
                catch (Exception ex) when (!Debugger.IsAttached)
                {
                    _logger.LogError(ex, "Error during index loading");
                    Exeption = ex;
                    Index = new CrawledIndex();
                    IndexStats = CreateIndexStats(Index);
                    ProgressText = string.Empty;
                }
            });

            static IReadOnlyList<RepoStats> CreateIndexStats(CrawledIndex index)
            {
                var indexStats = new List<RepoStats>();

                foreach (var repo in index.Repos)
                {
                    var repoStats = new RepoStats
                    {
                        Org = repo.Org,
                        Repo = repo.Name,
                        LastUpdatedAt = GetLastUpdatedAt(repo),
                        NumberOfIssues = repo.Issues.Count
                    };

                    indexStats.Add(repoStats);
                }

                return indexStats.ToArray();
            }
        }

        private static DateTimeOffset? GetLastUpdatedAt(CrawledRepo repo)
        {
            if (!repo.Issues.Any())
                return null;

            return repo.Issues.Values.Max(i => i.UpdatedAt ?? i.CreatedAt);
        }

        public Exception Exeption { get; private set; }

        public CrawledIndex Index { get; private set; }

        public IReadOnlyList<RepoStats> IndexStats { get; private set; }

        public string ProgressText
        {
            get => _progress;
            private set
            {
                if (_progress != value)
                {
                    _progress = value;
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler Changed;
    }
}
