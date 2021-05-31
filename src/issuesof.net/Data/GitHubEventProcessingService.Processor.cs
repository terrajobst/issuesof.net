using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using IssueDb;
using IssueDb.Crawling;

using Microsoft.Extensions.Logging;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Data
{
    public sealed partial class GitHubEventProcessingService
    {
        // TODO: We should change the trie/index such that all issues are referred by an int-ID so that we can update a single object reference
        //
        //       This avoids the problem where we need to update the data in-place as some fields on the issue can't be written atomically.
        //       This also avoids the problem where observers can see partially updated issues. If can just replace the entire issue object
        //       observers either the old issue or the new issue but never any partial state.

        private sealed class Processor : GitHubEventProcessor
        {
            private readonly ILogger _logger;
            private readonly IndexService _indexService;

            public Processor(ILogger logger, IndexService indexService)
            {
                _logger = logger;
                _indexService = indexService;
            }

            public override void ProcessMessage(GitHubEventMessage message)
            {
                _logger.LogInformation($"Processing message {message}");

                try
                {
                    base.ProcessMessage(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing message {message}");
                }
            }

            protected override void ProcessRepoMessage(GitHubEventMessage message, GitHubEventRepository repository, GitHubEventRepoAction action)
            {
                switch (action)
                {
                    case GitHubEventRepoAction.Created:
                        AddRepo(repository);
                        break;
                    case GitHubEventRepoAction.Deleted:
                        RemoveRepo(repository);
                        break;
                    case GitHubEventRepoAction.Archived:
                    case GitHubEventRepoAction.Unarchived:
                    case GitHubEventRepoAction.Publicized:
                    case GitHubEventRepoAction.Privatized:
                        UpdateRepo(repository);
                        break;
                }
            }

            protected override void ProcessLabelMessage(GitHubEventMessage message, GitHubEventRepository repository, GitHubEventLabel label, GitHubEventLabelAction action)
            {
                switch (action)
                {
                    case GitHubEventLabelAction.Created:
                        AddLabel(repository, label);
                        break;
                    case GitHubEventLabelAction.Edited:
                        UpdateLabel(repository, label);
                        break;
                    case GitHubEventLabelAction.Deleted:
                        RemoveLabel(repository, label);
                        break;
                }
            }

            protected override void ProcessMilestoneMessage(GitHubEventMessage message, GitHubEventRepository repository, GitHubEventMilestone milestone, GitHubEventMilestoneAction action)
            {
                switch (action)
                {
                    case GitHubEventMilestoneAction.Created:
                        AddMilestone(repository, milestone);
                        break;
                    case GitHubEventMilestoneAction.Edited:
                    case GitHubEventMilestoneAction.Opened:
                    case GitHubEventMilestoneAction.Closed:
                        UpdateMilestone(repository, milestone);
                        break;
                    case GitHubEventMilestoneAction.Deleted:
                        RemoveMilestone(repository, milestone);
                        break;
                }
            }

            protected override void ProcessIssueMessage(GitHubEventMessage message, GitHubEventRepository repository, GitHubEventIssue issue, GitHubEventIssueAction action)
            {
                switch (action)
                {
                    case GitHubEventIssueAction.Opened:
                        AddIssueOrPullRequest(repository, issue);
                        break;
                    case GitHubEventIssueAction.Closed:
                    case GitHubEventIssueAction.Reopened:
                    case GitHubEventIssueAction.Edited:
                    case GitHubEventIssueAction.Assigned:
                    case GitHubEventIssueAction.Unassigned:
                    case GitHubEventIssueAction.Labeled:
                    case GitHubEventIssueAction.Unlabeled:
                    case GitHubEventIssueAction.Milestoned:
                    case GitHubEventIssueAction.Demilestoned:
                    case GitHubEventIssueAction.Locked:
                    case GitHubEventIssueAction.Unlocked:
                        UpdateIssue(repository, issue);
                        break;
                    case GitHubEventIssueAction.Deleted:
                        RemoveIssue(repository, issue);
                        break;
                    case GitHubEventIssueAction.Transferred:
                        TransferIssue(repository, issue, message.Body.Changes.NewRepository, message.Body.Changes.NewIssue);
                        break;
                }
            }

            protected override void ProcessPullRequestMessage(GitHubEventMessage message, GitHubEventRepository repository, GitHubEventPullRequest pullRequest, GitHubEventPullRequestAction action)
            {
                switch (action)
                {
                    case GitHubEventPullRequestAction.Opened:
                        AddIssueOrPullRequest(repository, pullRequest);
                        break;
                    case GitHubEventPullRequestAction.Closed:
                    case GitHubEventPullRequestAction.Reopened:
                    case GitHubEventPullRequestAction.Edited:
                    case GitHubEventPullRequestAction.Assigned:
                    case GitHubEventPullRequestAction.Unassigned:
                    case GitHubEventPullRequestAction.Labeled:
                    case GitHubEventPullRequestAction.Unlabeled:
                    case GitHubEventPullRequestAction.Locked:
                    case GitHubEventPullRequestAction.Unlocked:
                    case GitHubEventPullRequestAction.ConvertedToDraft:
                    case GitHubEventPullRequestAction.ReadyForReview:
                        UpdatePullRequest(repository, pullRequest);
                        break;
                }
            }

            private void AddRepo(GitHubEventRepository repository)
            {
                if (repository.Private)
                    return;

                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = new CrawledRepo();

                UpdateRepo(repository, crawledRepo);

                index.Repos = index.Repos.CopyAndAdd(crawledRepo);

                _indexService.NotifyIndexChanged();
            }

            private void UpdateRepo(GitHubEventRepository repository)
            {
                if (repository.Private)
                {
                    RemoveRepo(repository);
                    return;
                }

                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                UpdateRepo(repository, crawledRepo);

                _indexService.NotifyIndexChanged();
            }

            private static void UpdateRepo(GitHubEventRepository repository, CrawledRepo crawledRepo)
            {
                crawledRepo.Org = repository.Owner.Login;
                crawledRepo.Name = repository.Name;
                crawledRepo.IsArchived = repository.Archived;
            }

            private void RemoveRepo(GitHubEventRepository repository)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                foreach (var issue in crawledRepo.Issues.Values)
                    RemoveIssue(crawledRepo, issue);

                index.Repos = index.Repos.CopyAndRemove(crawledRepo);

                _indexService.NotifyIndexChanged();
            }

            private void AddLabel(GitHubEventRepository repository, GitHubEventLabel label)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                CreateLabel(crawledRepo, label);

                _indexService.NotifyIndexChanged();
            }

            private static CrawledLabel CreateLabel(CrawledRepo crawledRepo, GitHubEventLabel label)
            {
                var crawledLabel = new CrawledLabel();
                UpdateLabel(label, crawledLabel);

                crawledRepo.Labels = crawledRepo.Labels.CopyAndAdd(crawledLabel);

                return crawledLabel;
            }

            private void UpdateLabel(GitHubEventRepository repository, GitHubEventLabel label)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                var crawledLabel = crawledRepo.Labels.SingleOrDefault(l => l.Id == label.Id);
                if (crawledLabel is null)
                    return;

                var oldName = crawledLabel.Name;

                UpdateLabel(label, crawledLabel);

                var newName = crawledLabel.Name;

                if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
                {
                    var removedTrieTerms = new[] { $"label:{oldName}" };
                    var addedTrieTerms = new[] { $"label:{newName}" };

                    foreach (var issue in crawledRepo.Issues.Values)
                    {
                        RemoveTrieTerms(issue, removedTrieTerms);
                        AddTrieTerms(issue, addedTrieTerms);
                    }
                }

                _indexService.NotifyIndexChanged();
            }

            private static void UpdateLabel(GitHubEventLabel label, CrawledLabel crawledLabel)
            {
                crawledLabel.ColorText = label.Color;
                crawledLabel.Description = label.Description;
                crawledLabel.Name = label.Name;
            }

            private void RemoveLabel(GitHubEventRepository repository, GitHubEventLabel label)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                var crawledLabel = crawledRepo.Labels.SingleOrDefault(l => l.Id == label.Id);
                if (crawledLabel is null)
                    return;

                crawledRepo.Labels = crawledRepo.Labels.CopyAndRemove(crawledLabel);

                var removedTrieTerms = new[] { $"label:{crawledLabel.Name}" };

                foreach (var issue in crawledRepo.Issues.Values)
                    RemoveTrieTerms(issue, removedTrieTerms);

                _indexService.NotifyIndexChanged();
            }

            private CrawledLabel GetOrCreateLabel(GitHubEventRepository repository, GitHubEventLabel label)
            {
                var index = _indexService.Index;
                Debug.Assert(index is not null);

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                Debug.Assert(crawledRepo is not null);

                var crawledLabel = crawledRepo.Labels.SingleOrDefault(l => l.Id == label.Id);
                return crawledLabel is not null ? crawledLabel : CreateLabel(crawledRepo, label);
            }

            private void AddMilestone(GitHubEventRepository repository, GitHubEventMilestone milestone)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                CreateMilestone(crawledRepo, milestone);

                _indexService.NotifyIndexChanged();
            }

            private static CrawledMilestone CreateMilestone(CrawledRepo crawledRepo, GitHubEventMilestone milestone)
            {
                var crawledMilestone = new CrawledMilestone();
                UpdateMilestone(milestone, crawledMilestone);

                crawledRepo.Milestones = crawledRepo.Milestones.CopyAndAdd(crawledMilestone);

                return crawledMilestone;
            }

            private void UpdateMilestone(GitHubEventRepository repository, GitHubEventMilestone milestone)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                var crawledMilestone = crawledRepo.Milestones.SingleOrDefault(m => m.Id == m.Id);
                if (crawledMilestone is null)
                    return;

                var oldTitle = crawledMilestone.Title;

                UpdateMilestone(milestone, crawledMilestone);

                var newTitle = crawledMilestone.Title;

                if (!string.Equals(oldTitle, newTitle, StringComparison.OrdinalIgnoreCase))
                {
                    var removedTrieTerms = new[] { $"milestone:{oldTitle}" };
                    var addedTrieTerms = new[] { $"milestone:{newTitle}" };

                    foreach (var issue in crawledRepo.Issues.Values)
                    {
                        RemoveTrieTerms(issue, removedTrieTerms);
                        AddTrieTerms(issue, addedTrieTerms);
                    }
                }

                _indexService.NotifyIndexChanged();
            }

            private static void UpdateMilestone(GitHubEventMilestone milestone, CrawledMilestone crawledMilestone)
            {
                crawledMilestone.Title = milestone.Title;
                crawledMilestone.Description = milestone.Description;
            }

            private void RemoveMilestone(GitHubEventRepository repository, GitHubEventMilestone milestone)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                var crawledMilestone = crawledRepo.Milestones.SingleOrDefault(m => m.Id == milestone.Id);
                if (crawledMilestone is null)
                    return;

                crawledRepo.Milestones = crawledRepo.Milestones.CopyAndRemove(crawledMilestone);

                var removedTrieTerms = new[] { $"milestone:{crawledMilestone.Title}" };

                foreach (var issue in crawledRepo.Issues.Values)
                    RemoveTrieTerms(issue, removedTrieTerms);

                _indexService.NotifyIndexChanged();
            }

            private CrawledMilestone GetOrCreateMilestone(GitHubEventRepository repository, GitHubEventMilestone milestone)
            {
                if (milestone is null)
                    return null;

                var index = _indexService.Index;
                Debug.Assert(index is not null);

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                Debug.Assert(crawledRepo is not null);

                var crawledMilestone = crawledRepo.Milestones.SingleOrDefault(m => m.Id == milestone.Id);
                return crawledMilestone is not null ? crawledMilestone : CreateMilestone(crawledRepo, milestone);
            }

            private void AddIssueOrPullRequest(GitHubEventRepository repository, GitHubEventIssueOrPullRequest issueOrPullRequest)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                var crawledIssue = new CrawledIssue();
                crawledIssue.Repo = crawledRepo;
                crawledIssue.Number = issueOrPullRequest.Number;
                crawledIssue.CreatedAt = issueOrPullRequest.CreatedAt;
                crawledIssue.CreatedBy = issueOrPullRequest.User.Login;
                crawledIssue.IsPullRequest = issueOrPullRequest is GitHubEventPullRequest;

                UpdateIssueOrPullRequest(repository, issueOrPullRequest, crawledIssue);

                crawledRepo.Issues[crawledIssue.Number] = crawledIssue;

                AddTrieTerms(crawledIssue, crawledIssue.GetTrieTerms());

                _indexService.NotifyIndexChanged();
            }

            private void UpdateIssue(GitHubEventRepository repository, GitHubEventIssue issue)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                if (!crawledRepo.Issues.TryGetValue(issue.Number, out var crawledIssue))
                    return;

                var oldTrieTerms = crawledIssue.GetTrieTerms();

                UpdateIssueOrPullRequest(repository, issue, crawledIssue);
                UpdateTrie(crawledIssue, oldTrieTerms);

                _indexService.NotifyIndexChanged();
            }

            private void UpdateIssueOrPullRequest(GitHubEventRepository repository, GitHubEventIssueOrPullRequest issueOrPullRequest, CrawledIssue crawledIssue)
            {
                crawledIssue.IsOpen = issueOrPullRequest.State == "open";
                crawledIssue.Title = issueOrPullRequest.Title;
                crawledIssue.IsLocked = issueOrPullRequest.Locked;
                crawledIssue.UpdatedAt = issueOrPullRequest.UpdatedAt; // TODO: Non-atomic write
                crawledIssue.ClosedAt = issueOrPullRequest.ClosedAt;   // TODO: Non-atomic write
                crawledIssue.Assignees = issueOrPullRequest.Assignees.Select(a => a.Login).ToArray();
                crawledIssue.Labels = issueOrPullRequest.Labels.Select(l => GetOrCreateLabel(repository, l)).ToArray();
                crawledIssue.Milestone = GetOrCreateMilestone(repository, issueOrPullRequest.Milestone);

                if (issueOrPullRequest is GitHubEventPullRequest pullRequest)
                {
                    crawledIssue.IsMerged = pullRequest.Merged;
                    crawledIssue.IsDraft = pullRequest.Draft;
                }
            }

            private void TransferIssue(GitHubEventRepository repository, GitHubEventIssue issue, GitHubEventRepository newRepository, GitHubEventIssue newIssue)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                if (!crawledRepo.Issues.TryGetValue(issue.Number, out var crawledIssue))
                    return;

                var newCrawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (newCrawledRepo is null)
                    return;

                var oldTrieTerms = crawledIssue.GetTrieTerms();

                crawledRepo.Issues.Remove(crawledIssue.Number);
                crawledIssue.Number = newIssue.Number;
                crawledIssue.Repo = newCrawledRepo;
                newCrawledRepo.Issues[crawledIssue.Number] = crawledIssue;

                UpdateIssueOrPullRequest(newRepository, newIssue, crawledIssue);
                UpdateTrie(crawledIssue, oldTrieTerms);

                _indexService.NotifyIndexChanged();
            }

            private void RemoveIssue(GitHubEventRepository repository, GitHubEventIssue issue)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                if (!crawledRepo.Issues.TryGetValue(issue.Number, out var crawledIssue))
                    return;

                RemoveIssue(crawledRepo, crawledIssue);

                _indexService.NotifyIndexChanged();
            }

            private void RemoveIssue(CrawledRepo crawledRepo, CrawledIssue crawledIssue)
            {
                var oldTrieTerms = crawledIssue.GetTrieTerms();

                crawledRepo.Issues.Remove(crawledIssue.Number);

                RemoveTrieTerms(crawledIssue, oldTrieTerms);
            }

            private void UpdatePullRequest(GitHubEventRepository repository, GitHubEventPullRequest pullRequest)
            {
                var index = _indexService.Index;
                if (index is null)
                    return;

                var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
                if (crawledRepo is null)
                    return;

                if (!crawledRepo.Issues.TryGetValue(pullRequest.Number, out var crawledIssue))
                    return;

                var oldTrieTerms = crawledIssue.GetTrieTerms();

                UpdateIssueOrPullRequest(repository, pullRequest, crawledIssue);
                UpdateTrie(crawledIssue, oldTrieTerms);

                _indexService.NotifyIndexChanged();
            }

            private void UpdateTrie(CrawledIssue issue, IEnumerable<string> oldTerms)
            {
                var trie = _indexService.Index?.Trie;
                if (trie is null)
                    return;

                var newTerms = issue.GetTrieTerms();

                var removedTerms = oldTerms.Where(t => !newTerms.Contains(t));
                var addedTerms = newTerms.Where(t => !oldTerms.Contains(t));

                RemoveTrieTerms(issue, removedTerms);
                AddTrieTerms(issue, addedTerms);
            }

            private void AddTrieTerms(CrawledIssue issue, IEnumerable<string> terms)
            {
                var trie = _indexService.Index?.Trie;
                if (trie is null)
                    return;

                foreach (var term in terms)
                    trie.Add(term, issue);
            }

            private void RemoveTrieTerms(CrawledIssue issue, IEnumerable<string> terms)
            {
                var trie = _indexService.Index?.Trie;
                if (trie is null)
                    return;

                foreach (var term in terms)
                    trie.Remove(term, issue);
            }
        }
    }
}
