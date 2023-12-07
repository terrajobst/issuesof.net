using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure.Storage.Blobs;

using IssueDb.Crawling;

using IssuesOfDotNet.net.Data;

namespace IssuesOfDotNet.Data;

public sealed class IndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    private string? _progress;

    public IndexService(ILogger<IndexService> logger,
                        IConfiguration configuration,
                        IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        Reload();
    }

    [MemberNotNull(nameof(IndexStats))]
    public void Reload()
    {
        Exception = null;
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
                    var binDirectory = Path.GetDirectoryName(GetType().Assembly.Location)!;
                    var indexFile = Path.Combine(binDirectory, indexName);

                    if (!File.Exists(indexFile))
                    {
                        ProgressText = $"Downloading index...";
                        await blobClient.DownloadToAsync(indexFile);
                    }

                    ProgressText = "Loading index...";
                    Index = await CrawledIndex.LoadAsync(indexFile);
                }

                Exception = null;
                ProgressText = null;
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                _logger.LogError(ex, "Error during index loading");
                Exception = ex;
                Index = new CrawledIndex();
                ProgressText = string.Empty;
            }
        });
    }

    private void UpdateIndex()
    {
        IndexStats = CreateIndexStats(Index);

        static IReadOnlyList<RepoStats> CreateIndexStats(CrawledIndex? index)
        {
            var indexStats = new List<RepoStats>();

            if (index is not null)
            {
                foreach (var repo in index.Repos)
                {
                    var repoStats = new RepoStats
                    {
                        Org = repo.Org,
                        Repo = repo.Name,
                        Size = repo.Size,
                        LastUpdatedAt = repo.IncrementalUpdateStart,
                        NumberOfOpenIssues = repo.Issues.Values.Count(i => i.IsOpen),
                        NumberOfIssues = repo.Issues.Count
                    };

                    indexStats.Add(repoStats);
                }
            }

            return indexStats.ToArray();
        }
    }

    public Exception? Exception { get; private set; }

    public CrawledIndex? Index { get; private set; }

    public IReadOnlyList<RepoStats> IndexStats { get; private set; }

    public string? ProgressText
    {
        get => _progress;
        private set
        {
            if (_progress != value)
            {
                _progress = value;
                NotifyIndexChanged();
                NotifyProgressChanged();
            }
        }
    }

    public void NotifyIndexChanged()
    {
        UpdateIndex();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyProgressChanged()
    {
        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public event EventHandler? ProgressChanged;
}
