using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Azure.Storage.Blobs;

using IssueDb.Crawling;

using IssuesOfDotNet.net.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IssuesOfDotNet.Data
{
    public sealed class IndexService
    {
        private readonly ILogger<IndexService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private string _progress;

        public IndexService(ILogger<IndexService> logger,
                            IConfiguration configuration,
                            IWebHostEnvironment environment)
        {
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
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
                    _logger.LogInformation("Loading index");

                    var azureConnectionString = _configuration["AzureStorageConnectionString"];
                    var indexName = "index.cicache";
                    var blobClient = new BlobClient(azureConnectionString, "index", indexName);

                    if (!_environment.IsDevelopment())
                    {
                        using var memoryStream = new MemoryStream();
                        ProgressText = $"Downloading index...";
                        await blobClient.DownloadToAsync(memoryStream);
                        memoryStream.Position = 0;
                        ProgressText = "Loading index...";
                        Index = await CrawledIndex.LoadAsync(memoryStream);
                    }
                    else
                    {
                        var binDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
                        var indexFile = Path.Combine(binDirectory, indexName);

                        if (!File.Exists(indexFile))
                        {
                            ProgressText = $"Downloading index...";
                            await blobClient.DownloadToAsync(indexFile);
                        }

                        ProgressText = "Loading index...";
                        Index = await CrawledIndex.LoadAsync(indexFile);
                    }

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
