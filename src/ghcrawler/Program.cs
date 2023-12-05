using System.Diagnostics;
using System.Text.Json;

using Azure.Storage.Blobs;

using IssueDb;
using IssueDb.Crawling;
using IssueDb.Eventing;

using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Primitives;

using Mono.Options;

using Octokit;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Crawler;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        //args = new[]
        //{
        //    "--subscriptions",
        //    //"dotnet/runtime",
        //    //"--reindex",
        //    //"--starting-repo",
        //    //"dotnet/docfx",
        //    "--no-pull-latest",
        //    "--no-upload",
        //    "--out",
        //    @"P:\issuesof.net\src\issuesof.net\bin\Debug\net5.0\index.cicache",
        //};

        var appName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
        var repoSpecs = new List<string>();
        var outputPath = "";
        var reindex = false;
        var pullLatest = true;
        var randomReindex = true;
        var uploadToAzure = true;
        var startingRepoName = (string)null;
        var help = args.Length == 0;
        var useSubscriptions = false;

        var options = new OptionSet
        {
            $"usage: {appName} <repo-spec>... [OPTIONS]+",
            $"",
            $"Examples:",
            $"      {appName} dotnet aspnet",
            $"",
            $"             Indexes all public repos in the dotnet and aspnet orgs",
            $"",
            $"      {appName} dotnet microsoft/CsWinRT microsoft/MSBuild",
            $"",
            $"             Indexes all public repos in the dotnet orgs and the CsWinRT",
            $"             and MSBuild repos in the Microsoft org.",
            $"",
            $"<repo-spec> can be of the following forms:",
            $"      owner                  Indexes all public repos of the owner",
            $"      owner/repo             Indexes only the one repo, if it is public",
            $"",
            $"Options:",
            { "subscriptions", "Indicates whether to use the built-in subscriptions or not", v => useSubscriptions = true },
            { "out=", "The output {path} the index should be written to", v => outputPath = v },
            { "reindex", "Specifies that the repo should be reindexed", v => reindex = true },
            { "starting-repo=", "The starting {repo} to re-index", v => startingRepoName = v },
            { "no-pull-latest", null, v => pullLatest = false, true },
            { "no-random-reindex", null, v => randomReindex = false, true },
            { "no-upload", null, v => uploadToAzure = false, true },
            { "h|?|help", null, v => help = true, true },
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

            var unprocessed = new List<string>();

            foreach (var parameter in parameters)
            {
                if (!char.IsLetter(parameter[0]))
                    unprocessed.Add(parameter);
                else
                    repoSpecs.Add(parameter);
            }

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

        var subscriptionList = useSubscriptions
            ? CrawledSubscriptionList.CreateDefault()
            : new CrawledSubscriptionList();

        foreach (var repoSpec in repoSpecs)
            subscriptionList.Add(repoSpec);

        if (reindex && !pullLatest)
        {
            Console.Error.WriteLine($"error: --reindex can't be combined with --no-pull-latest");
            return 1;
        }

        if (startingRepoName is not null && !reindex)
        {
            Console.Error.WriteLine($"error: --starting-repo can't be used unless --reindex is specified");
            return 1;
        }

        try
        {
            await RunAsync(subscriptionList, reindex, pullLatest, randomReindex, uploadToAzure, startingRepoName, outputPath);
            return 0;
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            Console.WriteLine($"fatal: {ex}");
            return 1;
        }
    }

    private static async Task RunAsync(CrawledSubscriptionList subscriptionList, bool reindex, bool pullLatest, bool randomReindex, bool uploadToAzure, string startingRepoName, string outputPath)
    {
        var reindexIntervalInDays = 28;
        var today = DateTime.Today;

        var connectionString = GetAzureStorageConnectionString();

        // TODO: We should avoid having to use a temp directory

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ghcrawler");
        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);

        Directory.CreateDirectory(tempDirectory);

        var cacheContainerName = "cache";
        var cacheContainerClient = new BlobContainerClient(connectionString, cacheContainerName);

        if (!reindex || startingRepoName is not null)
        {
            var startingBlobName = $"{startingRepoName}.crcache";
            var reachedStartingBlob = false;

            await foreach (var blob in cacheContainerClient.GetBlobsAsync())
            {
                if (!subscriptionList.Contains(blob.Name.Replace(".crcache", "")))
                    continue;

                if (blob.Name == startingBlobName)
                    reachedStartingBlob = true;

                if (reachedStartingBlob)
                    continue;

                Console.WriteLine($"Downloading {blob.Name}...");

                var localPath = Path.Combine(tempDirectory, blob.Name);
                var localDirectory = Path.GetDirectoryName(localPath);
                Directory.CreateDirectory(localDirectory);

                var blobClient = new BlobClient(connectionString, cacheContainerName, blob.Name);
                await blobClient.DownloadToAsync(localPath);
            }
        }

        var client = CreateGitHubAppClient();

        // Loading repos

        await client.UseInstallationTokenAsync("dotnet");

        var jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        var repos = new List<CrawledRepo>();

        var reachedStartingRepo = reindex && startingRepoName is null;

        foreach (var org in subscriptionList.Orgs)
        {
            var orgDirectory = Path.Join(tempDirectory, org);
            Directory.CreateDirectory(orgDirectory);

            var existingRepos = Directory.GetFiles(orgDirectory, "*.crcache")
                                         .Select(p => Path.GetFileNameWithoutExtension(p));

            if (!pullLatest)
            {
                Console.WriteLine($"Loading repos for {org}...");

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
                Console.WriteLine($"Requesting repos for {org}...");
                var availableRepos = await RequestReposAsync(client, org);

                var deletedRepos = existingRepos.ToHashSet(StringComparer.OrdinalIgnoreCase);
                deletedRepos.ExceptWith(availableRepos.Select(r => r.Name));

                foreach (var deletedRepo in deletedRepos)
                {
                    var blobName = $"{org}/{deletedRepo}.crcache";
                    var repoPath = Path.Join(tempDirectory, blobName);

                    Console.WriteLine($"Deleting local file {blobName}...");
                    File.Delete(repoPath);

                    if (uploadToAzure)
                    {
                        Console.WriteLine($"Deleting Azure blob {blobName}...");
                        await cacheContainerClient.DeleteBlobAsync(blobName);
                    }
                }

                foreach (var repo in availableRepos)
                {
                    if (!subscriptionList.Contains(org, repo.Name))
                        continue;

                    var blobName = $"{org}/{repo.Name}.crcache";
                    var repoPath = Path.Join(tempDirectory, blobName);

                    if (string.Equals($"{org}/{repo.Name}", startingRepoName, StringComparison.OrdinalIgnoreCase))
                        reachedStartingRepo = true;

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
                            Id = repo.Id,
                            Org = org,
                            Name = repo.Name
                        };
                    }

                    crawledRepo.IsArchived = repo.Archived;
                    crawledRepo.Size = repo.Size;

                    repos.Add(crawledRepo);

                    var repoIsDueForReindexing = crawledRepo.LastReindex is null ||
                                                 crawledRepo.LastReindex?.AddDays(reindexIntervalInDays) <= today;

                    if (reachedStartingRepo)
                        Console.WriteLine($"Marking {repo.FullName} to be re-indexed because we reached the starting repo {startingRepoName}.");

                    if (repoIsDueForReindexing)
                    {
                        if (crawledRepo.LastReindex is null)
                            Console.WriteLine($"Marking {repo.FullName} to be re-indexed because it was never fully indexed.");
                        else
                            Console.WriteLine($"Marking {repo.FullName} to be re-indexed because it was more than {reindexIntervalInDays} days ago, on {crawledRepo.LastReindex}.");
                    }

                    if (reachedStartingRepo || repoIsDueForReindexing)
                        crawledRepo.Clear();
                }
            }
        }

        // We want to ensure that all repos are fully-reindexed at least once every four weeks.
        // That means we need to reindex at least #Repos / 28 per day.
        //
        // On top of that, we need to ensure that all repos which were never fully indexed (e.g.
        // they are new or were forced to be reindexed) are also reindexed.

        if (randomReindex)
        {
            var reposThatNeedReindexing = repos.Where(r => r.LastReindex is null).ToHashSet();

            var minimumNumberOfReposToBeReindexed = (int)Math.Ceiling(repos.Count / (float)reindexIntervalInDays);
            var numberOfReposThatNeedReindexing = reposThatNeedReindexing.Count;

            if (numberOfReposThatNeedReindexing < minimumNumberOfReposToBeReindexed)
            {
                // OK, there are fewer repos that need reindexing than what we want to reindex
                // per day. So let's randomly pick some repos to reindex.

                var remainingRepos = repos.Except(reposThatNeedReindexing).ToList();
                var choiceCount = minimumNumberOfReposToBeReindexed - numberOfReposThatNeedReindexing;

                var random = new Random();

                for (var choice = 0; choice < choiceCount; choice++)
                {
                    var i = random.Next(0, remainingRepos.Count);
                    var repo = remainingRepos[i];

                    Console.WriteLine($"Marking {repo.FullName} to be re-indexed because it was randomly chosen.");

                    repo.Clear();
                    reposThatNeedReindexing.Add(repo);
                    remainingRepos.RemoveAt(i);
                }
            }
        }

        if (pullLatest)
        {
            Console.WriteLine($"Listing events...");

            var eventStore = new GitHubEventStore(connectionString);
            var events = await eventStore.ListAsync();

            Console.WriteLine($"Crawling {repos.Count:N0} repos, fully reindexing {repos.Count(r => r.LastReindex is null):N0} repos...");

            foreach (var crawledRepo in repos)
            {
                var blobName = $"{crawledRepo.FullName}.crcache";
                var repoPath = Path.Join(tempDirectory, blobName);
                var since = crawledRepo.IncrementalUpdateStart;

                var messages = new List<GitHubEventMessage>();

                if (since is null)
                {
                    Console.WriteLine($"Crawling {crawledRepo.FullName}...");
                }
                else
                {
                    var toBeDownloaded = events.Where(n => string.Equals(n.Org, crawledRepo.Org, StringComparison.OrdinalIgnoreCase) &&
                                                           string.Equals(n.Repo, crawledRepo.Name, StringComparison.OrdinalIgnoreCase))
                                               .ToArray();

                    if (toBeDownloaded.Any())
                    {
                        Console.WriteLine($"Loading {toBeDownloaded.Length:N0} events for {crawledRepo.FullName}...");

                        var i = 0;
                        var lastPercent = 0;

                        foreach (var name in toBeDownloaded)
                        {
                            var percent = (int)Math.Ceiling((float)i / toBeDownloaded.Length * 100);
                            i++;
                            if (percent % 10 == 0)
                            {
                                if (percent != lastPercent)
                                    Console.Write($"{percent}%...");

                                lastPercent = percent;
                            }

                            var payload = await eventStore.LoadAsync(name);
                            var headers = payload.Headers.ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value.ToArray()));
                            var body = payload.Body;
                            var message = GitHubEventMessage.Parse(headers, body);
                            messages.Add(message);
                        }

                        Console.WriteLine("done.");
                    }

                    Console.WriteLine($"Crawling {crawledRepo.FullName} since {since}...");
                }

                if (crawledRepo.LastReindex is null)
                    crawledRepo.LastReindex = DateTimeOffset.UtcNow;

                crawledRepo.AreaOwnership = await AreaOwnershipLoader.FromRepoAsync(crawledRepo.Org, crawledRepo.Name);

                var currentLabels = await RequestLabelsAsync(client, crawledRepo.Org, crawledRepo.Name);

                SyncLabels(crawledRepo, currentLabels, out var labelById);

                var currentMilestones = await RequestMilestonesAsync(client, crawledRepo.Org, crawledRepo.Name);

                SyncMilestones(crawledRepo, currentMilestones, out var milestoneById);

                // NOTE: GitHub's Issues.GetAllForeRepository() doesn't include issues that were transferred
                //
                // That's the good part. The bad part is that for the new repository where
                // it shows up, we have no way of knowing which repo it came from and which
                // number it used to have (even when looking at the issues timeline data),
                // so we can't remove the issue from the source repo.
                //
                // However, since we're persisting GitHub events we received, we'll can look
                // up which issues were transferred and remove them from the repo. This avoids
                // having to wait until we fully reindex the repo.
                //
                // Note, we remove transferred issues before pulling issues in case the issues
                // were being transferred back; it seems GitHub is reusing the numbers in that
                // case.

                foreach (var message in messages.Where(m => m.Body.Action == "transferred"))
                {
                    Console.WriteLine($"Removing {message.Body?.Repository?.FullName}#{message.Body?.Issue?.Number}: {message.Body?.Issue?.Title}");

                    var number = message.Body?.Issue?.Number;
                    if (number is not null)
                        crawledRepo.Issues.Remove(number.Value);
                }

                foreach (var issue in await RequestIssuesAsync(client, crawledRepo.Org, crawledRepo.Name, since))
                {
                    var crawledIssue = ConvertIssue(crawledRepo, issue, labelById, milestoneById);
                    crawledRepo.Issues[issue.Number] = crawledIssue;
                }

                foreach (var pullRequest in await RequestPullRequestsAsync(client, crawledRepo.Org, crawledRepo.Name, since))
                {
                    if (crawledRepo.Issues.TryGetValue(pullRequest.Number, out var issue))
                        UpdateIssue(issue, pullRequest);

                    // TODO: Get PR reviews
                    // TODO: Get PR commits
                    // TODO: Get PR status
                }

                await crawledRepo.SaveAsync(repoPath);

                if (uploadToAzure)
                {
                    Console.WriteLine($"Uploading {blobName} to Azure...");
                    var repoClient = new BlobClient(connectionString, cacheContainerName, blobName);
                    await repoClient.UploadAsync(repoPath, overwrite: true);

                    // Delete all events associated with this repo.

                    var eventsToBeDeleted = events.Where(e => string.Equals($"{e.Org}/{e.Repo}", crawledRepo.FullName, StringComparison.OrdinalIgnoreCase))
                                                  .ToArray();

                    Console.WriteLine($"Deleting {eventsToBeDeleted.Length:N0} events for {crawledRepo.FullName}...");
                    await eventStore.DeleteAsync(eventsToBeDeleted);
                }
            }
        }

        // Merge area ownerships

        var areaOwnership = CrawledAreaOwnership.Empty;

        foreach (var repo in repos)
            areaOwnership = areaOwnership.Merge(repo.AreaOwnership);

        foreach (var repo in repos)
            repo.AreaOwnership = areaOwnership;

        // Do some consistency checking

        foreach (var repo in repos)
        {
            var milestones = repo.Milestones.ToHashSet();
            var labels = repo.Labels.ToHashSet();

            foreach (var issue in repo.Issues.Values)
            {
                foreach (var label in issue.Labels.Where(l => !labels.Contains(l)))
                {
                    Console.Error.WriteLine($"error: {repo.FullName}#{issue.Number}: label '{label.Name}' doesn't exist");
                }

                if (issue.Milestone is not null && !milestones.Contains(issue.Milestone))
                {
                    Console.Error.WriteLine($"error: {repo.FullName}#{issue.Number}: milestone '{issue.Milestone.Title}' doesn't exist");
                }
            }
        }

        Console.WriteLine("Creating trie...");

        var trie = new CrawledTrie<CrawledIssue>();
        var totalIssueCount = repos.Sum(r => r.Issues.Count);
        var lastPercentage = 0;
        var processedIssueCount = 0;

        foreach (var repo in repos)
        {
            foreach (var issue in repo.Issues.Values)
            {
                var percentage = (int)Math.Round((float)processedIssueCount++ / totalIssueCount * 100, 0);
                if (percentage != lastPercentage)
                {
                    Console.WriteLine($"{percentage}%...");
                    lastPercentage = percentage;
                }

                trie.Add(issue);
            }
        }

        Console.WriteLine("Creating index...");

        var index = new CrawledIndex()
        {
            AreaOwnership = areaOwnership,
            Repos = repos.ToList(),
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

    private static GitHubAppClient CreateGitHubAppClient()
    {
        var (appId, privateKey) = GetGitHubAppIdAndPrivateKey();
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        var productHeader = new ProductHeaderValue(name);
        return new GitHubAppClient(productHeader, appId, privateKey);
    }

    private static async Task<IReadOnlyList<Repository>> RequestReposAsync(GitHubAppClient client, string org)
    {
        var repos = await client.InvokeAsync(c => c.Repository.GetAllForOrg(org));
        return repos.OrderBy(r => r.Name)
                    .Where(r => r.Visibility == RepositoryVisibility.Public)
                    .ToArray();
    }

    private static Task<IReadOnlyList<Label>> RequestLabelsAsync(GitHubAppClient client, string org, string repo)
    {
        return client.InvokeAsync(c => c.Issue.Labels.GetAllForRepository(org, repo));
    }

    private static Task<IReadOnlyList<Milestone>> RequestMilestonesAsync(GitHubAppClient client, string org, string repo)
    {
        var request = new MilestoneRequest
        {
            State = ItemStateFilter.All
        };
        return client.InvokeAsync(c => c.Issue.Milestone.GetAllForRepository(org, repo, request));
    }

    private static Task<IReadOnlyList<Issue>> RequestIssuesAsync(GitHubAppClient client, string org, string repo, DateTimeOffset? since)
    {
        var issueRequest = new RepositoryIssueRequest()
        {
            SortProperty = IssueSort.Created,
            SortDirection = SortDirection.Ascending,
            State = ItemStateFilter.All,
            Since = since,
        };

        return client.InvokeAsync(c => c.Issue.GetAllForRepository(org, repo, issueRequest));
    }

    private static async Task<IReadOnlyList<PullRequest>> RequestPullRequestsAsync(GitHubAppClient client, string org, string repo, DateTimeOffset? since)
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

            var batch = await client.InvokeAsync(c => c.PullRequest.GetAllForRepository(org, repo, pullRequestRequest, options));
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

    private static void SyncLabels(CrawledRepo crawledRepo, IReadOnlyList<Label> gitHubLabels, out Dictionary<long, CrawledLabel> labelById)
    {
        // TODO: This logic feels similar to what we do in the web site. Should we reconcile this?

        var crawledLabelById = crawledRepo.Labels.ToDictionary(l => l.Id);
        var gitHubLabelById = gitHubLabels.ToDictionary(l => l.Id);

        foreach (var gitHubLabel in gitHubLabels)
        {
            if (crawledLabelById.TryGetValue(gitHubLabel.Id, out var crawledLabel))
            {
                // Update
                crawledLabel.Name = gitHubLabel.Name;
                crawledLabel.Description = gitHubLabel.Description;
                crawledLabel.ColorText = gitHubLabel.Color;
            }
            else
            {
                // Create
                crawledLabel = ConvertLabel(gitHubLabel);
                crawledRepo.Labels.Add(crawledLabel);
                crawledLabelById.Add(crawledLabel.Id, crawledLabel);
            }
        }

        // Delete

        var toBeDeleted = crawledRepo.Labels.Where(l => !gitHubLabelById.ContainsKey(l.Id))
                                            .ToArray();

        foreach (var crawledLabel in toBeDeleted)
        {
            crawledRepo.Labels.Remove(crawledLabel);

            foreach (var issue in crawledRepo.Issues.Values)
            {
                if (issue.Labels.Contains(crawledLabel))
                {
                    var newLabels = issue.Labels.ToList();
                    newLabels.Remove(crawledLabel);
                    issue.Labels = newLabels.ToArray();
                }
            }
        }

        // Fix labels

        foreach (var issue in crawledRepo.Issues.Values)
        {
            for (var i = 0; i < issue.Labels.Length; i++)
                issue.Labels[i] = crawledLabelById[issue.Labels[i].Id];
        }

        labelById = crawledLabelById;
    }

    private static void SyncMilestones(CrawledRepo crawledRepo, IReadOnlyList<Milestone> gitHubMilestones, out Dictionary<long, CrawledMilestone> milestoneById)
    {
        // TODO: This logic feels similar to what we do in the web site. Should we reconcile this?

        var crawledMilestoneById = crawledRepo.Milestones.ToDictionary(l => l.Id);
        var gitHubMilestoneById = gitHubMilestones.ToDictionary(l => l.Id);

        foreach (var gitHubMilestone in gitHubMilestones)
        {
            if (crawledMilestoneById.TryGetValue(gitHubMilestone.Id, out var crawledMilestone))
            {
                // Update
                crawledMilestone.Title = gitHubMilestone.Title;
                crawledMilestone.Description = gitHubMilestone.Description;
                crawledMilestone.Number = gitHubMilestone.Number;
            }
            else
            {
                // Create
                crawledMilestone = ConvertMilestone(gitHubMilestone);
                crawledRepo.Milestones.Add(crawledMilestone);
                crawledMilestoneById.Add(crawledMilestone.Id, crawledMilestone);
            }
        }

        // Delete

        var toBeDeleted = crawledRepo.Milestones.Where(l => !gitHubMilestoneById.ContainsKey(l.Id))
                                                .ToArray();

        foreach (var crawledMilestone in toBeDeleted)
        {
            crawledRepo.Milestones.Remove(crawledMilestone);

            foreach (var issue in crawledRepo.Issues.Values)
            {
                if (issue.Milestone == crawledMilestone)
                    issue.Milestone = null;
            }
        }

        // Fix milestones

        foreach (var issue in crawledRepo.Issues.Values)
        {
            if (issue.Milestone is not null)
                issue.Milestone = crawledMilestoneById[issue.Milestone.Id];
        }

        milestoneById = crawledMilestoneById;
    }

    private static CrawledLabel ConvertLabel(Label label)
    {
        return new CrawledLabel
        {
            Id = label.Id,
            Name = label.Name,
            Description = label.Description,
            ColorText = label.Color
        };
    }

    private static CrawledMilestone ConvertMilestone(Milestone milestone)
    {
        return new CrawledMilestone
        {
            Id = milestone.Id,
            Number = milestone.Number,
            Title = milestone.Title,
            Description = milestone.Description
        };
    }

    private static CrawledIssue ConvertIssue(CrawledRepo repo, Issue issue, Dictionary<long, CrawledLabel> labels, Dictionary<long, CrawledMilestone> milestones)
    {
        return new CrawledIssue
        {
            Id = issue.Id,
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
            Labels = GetLabels(issue.Labels, labels),
            Milestone = GetMilestone(issue.Milestone, milestones),
            IsLocked = issue.Locked,
            Comments = issue.Comments,
            ReactionsPlus1 = issue.Reactions?.Plus1 ?? 0,
            ReactionsMinus1 = issue.Reactions?.Minus1 ?? 0,
            ReactionsSmile = issue.Reactions?.Laugh ?? 0,
            ReactionsTada = issue.Reactions?.Hooray ?? 0,
            ReactionsThinkingFace = issue.Reactions?.Confused ?? 0,
            ReactionsHeart = issue.Reactions?.Heart ?? 0
            // TODO: RocketShip and Eyes are missing
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

    private static CrawledLabel[] GetLabels(IReadOnlyList<Label> labels, Dictionary<long, CrawledLabel> crawledLabels)
    {
        var result = new List<CrawledLabel>();
        foreach (var label in labels)
        {
            if (crawledLabels.TryGetValue(label.Id, out var crawledLabel))
                result.Add(crawledLabel);
        }

        return result.ToArray();
    }

    private static CrawledMilestone GetMilestone(Milestone milestone, Dictionary<long, CrawledMilestone> milestones)
    {
        if (milestone != null && milestones.TryGetValue(milestone.Id, out var crawledMilestone))
            return crawledMilestone;

        return null;
    }

    private static void UpdateIssue(CrawledIssue issue, PullRequest pullRequest)
    {
        issue.IsPullRequest = true;
        issue.IsDraft = pullRequest.Draft;
        issue.IsMerged = pullRequest.Merged;
        // TODO: pullRequest.MergedAt
        // TODO: pullRequest.Base?.Ref
        // TODO: pullRequest.Head?.Ref
        // TODO: pullRequest.RequestedReviewers
        // TODO: pullRequest.RequestedTeams
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

    private static (string AppId, string PrivateKey) GetGitHubAppIdAndPrivateKey()
    {
        var appId = Environment.GetEnvironmentVariable("GitHubAppId");
        var privateKey = Environment.GetEnvironmentVariable("GitHubAppPrivateKey");
        var secrets = Secrets.Load();

        if (string.IsNullOrEmpty(appId))
            appId = secrets?.GitHubAppId;

        if (string.IsNullOrEmpty(appId))
            throw new Exception("Cannot retreive secret 'GitHubAppId'. You either need to define an environment variable or a user secret.");

        if (string.IsNullOrEmpty(privateKey))
            privateKey = secrets?.GitHubAppPrivateKey;

        if (string.IsNullOrEmpty(privateKey))
            throw new Exception("Cannot retreive secret 'GitHubAppPrivateKey'. You either need to define an environment variable or a user secret.");

        return (appId, privateKey);
    }

    internal sealed class Secrets
    {
        public string AzureStorageConnectionString { get; set; }
        public string GitHubAppId { get; set; }
        public string GitHubAppPrivateKey { get; set; }

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
