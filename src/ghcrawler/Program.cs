using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Azure.Storage.Blobs;

using Microsoft.Extensions.Configuration.UserSecrets;

using Mono.Options;

using Octokit;

namespace IssuesOfDotNet.Crawler
{
    // TODO: For transferred issues, we should check whether we're ending up indexing issues
    //       twice. For example, there are no issues for repo CoreFX right now, but that's
    //       probably because they are all returned for runtime.
    //
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            //args = new[]
            //{
            //    "--org",
            //    "aspnet",
            //    "dotnet",
            //    "nuget",
            //    "--no-pull-latest",
            //    "--no-upload",
            //    "--out",
            //    @"P:\issuesof.net\src\issuesof.net\bin\Debug\net5.0\index.cicache",
            //};

            var appName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
            var orgs = new List<string>();
            var help = false;
            var activeTerms = (List<string>)null;
            var pullLatest = true;
            var uploadToAzure = true;
            var outputPath = "";

            var options = new OptionSet
            {
                $"usage: {appName} [OPTIONS]+",
                { "org", "The names of the GitHub orgs to index", v => activeTerms = orgs },
                { "out=", "The output path the index should be written to", v => outputPath = v },
                { "no-pull-latest", null, v => pullLatest = false, true },
                { "no-upload", null, v => uploadToAzure = false, true },
                { "h|?|help", null, v => help = true, true },
                { "<>", v => activeTerms?.Add(v) },
                new ResponseFileSource()
            };

            try
            {
                var parameters = options.Parse(args).ToArray();

                if (help)
                {
                    options.WriteOptionDescriptions(Console.Error);
                    return 0;
                }

                var unprocessed = parameters;

                if (unprocessed.Any())
                {
                    foreach (var option in unprocessed)
                        Console.Error.WriteLine($"error: unrecognized argument {option}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }

            if (orgs.Count == 0)
            {
                Console.Error.WriteLine($"error: must specify --org");
                return 1;
            }

            try
            {
                await RunAsync(orgs, pullLatest, uploadToAzure, outputPath);
                return 0;
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                Console.WriteLine($"fatal: {ex}");
                return 1;
            }
        }

        private static async Task RunAsync(IEnumerable<string> orgs, bool pullLatest, bool uploadToAzure, string outputPath)
        {
            var connectionString = GetAzureStorageConnectionString();
            var token = GetGitHubToken();

            // TODO: We should avoid having to use a temp directory

            var tempDirectory = Path.Combine(Path.GetTempPath(), "ghcrawler");
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);

            Directory.CreateDirectory(tempDirectory);

            var cacheContainerName = "cache";
            var cacheContainerClient = new BlobContainerClient(connectionString, cacheContainerName);

            await foreach (var blob in cacheContainerClient.GetBlobsAsync())
            {
                Console.WriteLine($"Downloading {blob.Name}...");

                var localPath = Path.Combine(tempDirectory, blob.Name);
                var localDirectory = Path.GetDirectoryName(localPath);
                Directory.CreateDirectory(localDirectory);

                var blobClient = new BlobClient(connectionString, cacheContainerName, blob.Name);
                await blobClient.DownloadToAsync(localPath);
            }

            var productInformation = new ProductHeaderValue("issuesof.net");
            var client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(token)
            };

            var jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            var repos = new List<CrawledRepo>();

            foreach (var org in orgs)
            {
                var orgDirectory = Path.Join(tempDirectory, org);
                Directory.CreateDirectory(orgDirectory);

                var existingRepos = Directory.GetFiles(orgDirectory, "*.crcache")
                                             .Select(p => Path.GetFileNameWithoutExtension(p));

                if (!pullLatest)
                {
                    foreach (var repoName in existingRepos)
                    {
                        var blobName = $"{repoName}.crcache";
                        var repoPath = Path.Join(orgDirectory, blobName);
                        var repo = await CrawledRepo.LoadAsync(repoPath);
                        if (repo is not null)
                            repos.Add(repo);
                    }
                }
                else
                {
                    var availableRepos = await RequestReposAsync(client, org);

                    var deletedRepos = existingRepos.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    deletedRepos.ExceptWith(availableRepos.Select(r => r.Name));

                    foreach (var deletedRepo in deletedRepos)
                    {
                        var blobName = $"{org}/{deletedRepo}.crcache";
                        var repoPath = Path.Join(tempDirectory, blobName);

                        Console.WriteLine($"Deleting {blobName}...");
                        File.Delete(repoPath);
                        await cacheContainerClient.DeleteBlobAsync(blobName);
                    }

                    foreach (var repo in availableRepos)
                    {
                        var blobName = $"{org}/{repo.Name}.crcache";
                        var repoPath = Path.Join(tempDirectory, blobName);

                        CrawledRepo crawledRepo;
                        try
                        {
                            crawledRepo = await CrawledRepo.LoadAsync(repoPath);
                        }
                        catch (JsonException)
                        {
                            Console.WriteLine($"WARNING: Couldn't parse {blobName}");
                            crawledRepo = null;
                        }

                        if (crawledRepo is null)
                        {
                            crawledRepo = new CrawledRepo
                            {
                                Org = org,
                                Name = repo.Name
                            };
                        }

                        repos.Add(crawledRepo);

                        var existingIssues = crawledRepo.Issues.Values;
                        var since = existingIssues.Any()
                                        ? existingIssues.Max(i => i.UpdatedAt ?? i.CreatedAt)
                                        : (DateTimeOffset?)null;

                        if (since is null)
                            Console.WriteLine($"Crawling {org}/{repo.Name}...");
                        else
                            Console.WriteLine($"Crawling {org}/{repo.Name} since {since}...");

                        crawledRepo.IsArchived = repo.Archived;

                        var labels = new Dictionary<string, CrawledLabel>();

                        foreach (var label in await RequestLabelsAsync(client, org, repo.Name))
                        {
                            var crawledLabel = ConvertLabel(label);
                            labels[label.Name] = crawledLabel;
                            crawledRepo.Labels.Add(crawledLabel);
                        }

                        var milestones = new Dictionary<int, CrawledMilestone>();

                        foreach (var milestone in await RequestMilestonesAsync(client, org, repo.Name))
                        {
                            var crawledMilestone = ConvertMilestone(milestone);
                            milestones[milestone.Number] = crawledMilestone;
                            crawledRepo.Milestones.Add(crawledMilestone);
                        }

                        foreach (var issue in await RequestIssuesAsync(client, org, repo.Name, since))
                        {
                            // NOTE: Sadly GitHub doesn't tell us the original issue number
                            //
                            //       That means we have no good way to remove the transferred issues. Double
                            //       sadly even the issue events don't reveal the original ID the issue was
                            //       transferred from.
                            //
                            //       We probably have to accept that we need to re-index everything every
                            //       once in a while to get rid of transferred issues.
                            if (IssueWasTransferred(crawledRepo, issue))
                                continue;

                            var crawledIssue = ConvertIssue(crawledRepo, issue, labels, milestones);
                            crawledRepo.Issues[issue.Number] = crawledIssue;
                        }

                        foreach (var pullRequest in await RequestPullRequestsAsync(client, org, repo.Name, since))
                        {
                            if (crawledRepo.Issues.TryGetValue(pullRequest.Number, out var issue))
                                UpdateIssue(issue, pullRequest);
                        }

                        await crawledRepo.SaveAsync(repoPath);

                        if (uploadToAzure)
                        {
                            Console.WriteLine($"Uploading {blobName} to Azure...");
                            var repoClient = new BlobClient(connectionString, cacheContainerName, blobName);
                            await repoClient.UploadAsync(repoPath, overwrite: true);
                        }
                    }
                }
            }

            Console.WriteLine("Creating trie...");

            var trie = new CrawledTrie();

            foreach (var repo in repos)
            {
                foreach (var issue in repo.Issues.Values)
                    trie.AddIssue(issue);
            }

            Console.WriteLine("Creating index...");

            var index = new CrawledIndex()
            {
                Repos = repos,
                Trie = trie
            };

            var indexName = "index.cicache";
            var indexPath = string.IsNullOrEmpty(outputPath)
                                ? Path.Join(tempDirectory, indexName)
                                : outputPath;

            await index.SaveAsync(indexPath);

            if (uploadToAzure)
            {
                Console.WriteLine("Uploading index to Azure...");

                var indexClient = new BlobClient(connectionString, "index", indexName);
                await indexClient.UploadAsync(indexPath, overwrite: true);
            }

            Console.WriteLine("Deleting temp files...");

            Directory.Delete(tempDirectory, recursive: true);
        }

        private static async Task<IReadOnlyList<Repository>> RequestReposAsync(GitHubClient client, string org)
        {
            var repos = await RetryOnRateLimiting(() => client.Repository.GetAllForOrg(org));
            return repos.OrderBy(r => r.Name)
                        .Where(r => r.Visibility == RepositoryVisibility.Public)
                        .ToArray();
        }

        private static Task<IReadOnlyList<Label>> RequestLabelsAsync(GitHubClient client, string org, string repo)
        {
            return RetryOnRateLimiting(() => client.Issue.Labels.GetAllForRepository(org, repo));
        }

        private static Task<IReadOnlyList<Milestone>> RequestMilestonesAsync(GitHubClient client, string org, string repo)
        {
            return RetryOnRateLimiting(() => client.Issue.Milestone.GetAllForRepository(org, repo));
        }

        private static Task<IReadOnlyList<Issue>> RequestIssuesAsync(GitHubClient client, string org, string repo, DateTimeOffset? since)
        {
            var issueRequest = new RepositoryIssueRequest()
            {
                SortProperty = IssueSort.Created,
                SortDirection = SortDirection.Ascending,
                State = ItemStateFilter.All,
                Since = since,
            };

            return RetryOnRateLimiting(() => client.Issue.GetAllForRepository(org, repo, issueRequest));
        }

        private static async Task<IReadOnlyList<PullRequest>> RequestPullRequestsAsync(GitHubClient client, string org, string repo, DateTimeOffset? since)
        {
            var pullRequestRequest = new PullRequestRequest()
            {
                SortProperty = PullRequestSort.Updated,
                SortDirection = SortDirection.Descending,
                State = ItemStateFilter.All,
            };

            var result = new List<PullRequest>();
            var page = 1;

            while (true)
            {
                var options = new ApiOptions
                {
                    StartPage = page,
                    PageSize = 100,
                    PageCount = 1
                };

                var batch = await RetryOnRateLimiting(() => client.PullRequest.GetAllForRepository(org, repo, pullRequestRequest, options));
                if (batch.Count == 0)
                    break;

                page++;

                var foundOlderOnes = false;

                foreach (var pr in batch)
                {
                    if (pr.UpdatedAt < since)
                    {
                        foundOlderOnes = true;
                    }
                    else
                    {
                        result.Add(pr);
                    }
                }

                if (foundOlderOnes)
                    break;
            }

            return result.ToArray();
        }

        private static async Task<T> RetryOnRateLimiting<T>(Func<Task<T>> func)
        {
            while (true)
            {
                try
                {
                    var result = await func();
                    return result;
                }
                catch (RateLimitExceededException ex)
                {
                    var padding = TimeSpan.FromMinutes(2);
                    var delay = ex.Reset - DateTimeOffset.Now + padding;
                    var time = ex.Reset + padding;
                    Console.WriteLine($"API rate limit exceeded. Waiting {delay.TotalMinutes:N0} minutes until it resets at {time.ToLocalTime():M/d/yyyy h:mm tt}...");
                    await Task.Delay(delay);
                    Console.WriteLine("Trying again...");
                }
            }
        }

        private static CrawledLabel ConvertLabel(Label label)
        {
            return new CrawledLabel
            {
                Name = label.Name,
                Description = label.Description,
                ForegroundColor = GetForegroundColor(label.Color),
                BackgroundColor = label.Color
            };
        }

        private static CrawledMilestone ConvertMilestone(Milestone milestone)
        {
            return new CrawledMilestone
            {
                Number = milestone.Number,
                Title = milestone.Title,
                Description = milestone.Description
            };
        }

        private static string GetForegroundColor(string backgroundColor)
        {
            var brightness = PerceivedBrightness(backgroundColor);
            var foregroundColor = brightness > 130 ? "black" : "white";
            return foregroundColor;
        }

        private static int PerceivedBrightness(string color)
        {
            var c = ParseColor(color);
            return (int)Math.Sqrt(
                c.R * c.R * .241 +
                c.G * c.G * .691 +
                c.B * c.B * .068);
        }

        private static Color ParseColor(string color)
        {
            if (!string.IsNullOrEmpty(color) && color.Length == 6 &&
                int.TryParse(color.Substring(0, 2), NumberStyles.HexNumber, null, out var r) &&
                int.TryParse(color.Substring(2, 2), NumberStyles.HexNumber, null, out var g) &&
                int.TryParse(color.Substring(4, 2), NumberStyles.HexNumber, null, out var b))
            {
                return Color.FromArgb(r, g, b);
            }

            return Color.Black;
        }

        private static bool IssueWasTransferred(CrawledRepo repo, Issue issue)
        {
            var match = Regex.Match(issue.Url, @"https://api.github.com/repos/(?<org>[^/]+)/(?<repo>[^/]+)/issues/(?<id>[^/]+)");
            if (match.Success)
            {
                var issueOrg = match.Groups["org"].Value;
                var issueRepo = match.Groups["repo"].Value;
                var matchesRepo = string.Equals(issueOrg, repo.Org, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(issueRepo, repo.Name, StringComparison.OrdinalIgnoreCase);

                if (!matchesRepo)
                    return true;
            }

            return false;
        }

        private static CrawledIssue ConvertIssue(CrawledRepo repo, Issue issue, Dictionary<string, CrawledLabel> labels, Dictionary<int, CrawledMilestone> milestones)
        {
            return new CrawledIssue
            {
                Repo = repo,
                Number = issue.Number,
                IsOpen = ConvertIssueState(issue.State),
                Title = issue.Title,
                Body = issue.Body,
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt,
                ClosedAt = issue.ClosedAt,
                CreatedBy = issue.User.Login,
                Assignees = ConvertUsers(issue.Assignees),
                Labels = ConvertLabels(issue.Labels, labels),
                Milestone = GetMilestone(issue.Milestone, milestones)
            };
        }

        private static bool ConvertIssueState(StringEnum<ItemState> state)
        {
            return state.Value switch
            {
                ItemState.Open => true,
                ItemState.Closed => false,
                _ => throw new NotImplementedException($"Unhandled issue state: '{state.StringValue}'"),
            };
        }

        private static string[] ConvertUsers(IReadOnlyList<User> assignees)
        {
            return assignees.Select(u => u.Login)
                            .ToArray();
        }

        private static CrawledLabel[] ConvertLabels(IReadOnlyList<Label> labels, Dictionary<string, CrawledLabel> crawledLabels)
        {
            var result = new List<CrawledLabel>();
            foreach (var label in labels)
            {
                if (crawledLabels.TryGetValue(label.Name, out var crawledLabel))
                    result.Add(crawledLabel);
            }

            return result.ToArray();
        }

        private static CrawledMilestone GetMilestone(Milestone milestone, Dictionary<int, CrawledMilestone> milestones)
        {
            if (milestone != null && milestones.TryGetValue(milestone.Number, out var crawledMilestone))
                return crawledMilestone;

            return null;
        }

        private static void UpdateIssue(CrawledIssue issue, PullRequest pullRequest)
        {
            issue.IsPullRequest = true;
            issue.IsDraft = pullRequest.Draft;
            issue.IsMerged = pullRequest.Merged;
        }

        private static string GetAzureStorageConnectionString()
        {
            var result = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
            if (string.IsNullOrEmpty(result))
            {
                var secrets = Secrets.Load();
                result = secrets?.AzureStorageConnectionString;
            }

            if (string.IsNullOrEmpty(result))
                throw new Exception("Cannot retreive secret 'AzureStorageConnectionString'. You either need to define an environment variable or a user secret.");

            return result;
        }

        private static string GetGitHubToken()
        {
            var result = Environment.GetEnvironmentVariable("GitHubToken");
            if (string.IsNullOrEmpty(result))
            {
                var secrets = Secrets.Load();
                result = secrets?.GitHubToken;
            }

            if (string.IsNullOrEmpty(result))
                throw new Exception("Cannot retreive secrete 'GitHubToken'. You either need to define an environment variable or a user secret.");

            return result;
        }

        internal sealed class Secrets
        {
            public string AzureStorageConnectionString { get; set; }
            public string GitHubToken { get; set; }

            public static Secrets Load()
            {
                var secretsPath = PathHelper.GetSecretsPathFromSecretsId("issuesof.net");
                if (!File.Exists(secretsPath))
                    return null;

                var secretsJson = File.ReadAllText(secretsPath);
                return JsonSerializer.Deserialize<Secrets>(secretsJson)!;
            }
        }
    }
}
