using System.Diagnostics;
using System.Text;

using IssueDb;
using IssueDb.Crawling;

using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.Label;
using Octokit.Webhooks.Events.Milestone;
using Octokit.Webhooks.Events.PullRequest;
using Octokit.Webhooks.Events.Repository;
using Octokit.Webhooks.Models;
using Octokit.Webhooks.Models.PullRequestEvent;

using Label = Octokit.Webhooks.Models.Label;

namespace IssuesOfDotNet.Data;

public sealed partial class GitHubEventProcessingService
{
    // TODO: We should change the trie/index such that all issues are referred by an int-ID so that we can update a single object reference
    //
    //       This avoids the problem where we need to update the data in-place as some fields on the issue can't be written atomically.
    //       This also avoids the problem where observers can see partially updated issues. If can just replace the entire issue object
    //       observers either the old issue or the new issue but never any partial state.

    private sealed class Processor : WebhookEventProcessor
    {
        private readonly ILogger _logger;
        private readonly IndexService _indexService;

        public Processor(ILogger logger, IndexService indexService)
        {
            _logger = logger;
            _indexService = indexService;
        }

        public override async Task ProcessWebhookAsync(WebhookHeaders headers, WebhookEvent webhookEvent)
        {
            {
                var sb = new StringBuilder();
                var args = new List<object>();

                sb.Append("Processing message");

                FormatMessage(headers, webhookEvent, sb, args);

                _logger.LogInformation(sb.ToString(), args.ToArray());
            }

            try
            {
                await base.ProcessWebhookAsync(headers, webhookEvent);
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                var args = new List<object>();

                sb.Append("Error processing message");

                FormatMessage(headers, webhookEvent, sb, args);

                _logger.LogError(ex, sb.ToString(), args.ToArray());
            }

            static void FormatMessage(WebhookHeaders headers, WebhookEvent message, StringBuilder sb, List<object> args)
            {
                sb.Append(", Delivery={Delivery}");
                args.Add(headers.Delivery);

                if (headers.Event is not null)
                {
                    sb.Append(", Event={Event}");
                    args.Add(headers.Event);
                }

                if (message.Action is not null)
                {
                    sb.Append(", Action={Action}");
                    args.Add(message.Action);
                }

                if (message.Organization is not null)
                {
                    sb.Append(", Org={Org}");
                    args.Add(message.Organization.Login);

                    sb.Append(", OrgId={OrgId}");
                    args.Add(message.Organization.Id);
                }

                if (message.Repository is not null)
                {
                    sb.Append(", Repo={Repo}");
                    args.Add(message.Repository.Name);

                    sb.Append(", RepoId={RepoId}");
                    args.Add(message.Repository.Id);
                }

                if (message is IssuesEvent issuesEvent)
                {
                    sb.Append(", Issue={Issue}");
                    args.Add(issuesEvent.Issue.Number);

                    sb.Append(", IssueId={IssueId}");
                    args.Add(issuesEvent.Issue.Id);
                }

                if (message is PullRequestEvent pullRequestEvent)
                {
                    sb.Append(", PullRequest={PullRequest}");
                    args.Add(pullRequestEvent.PullRequest.Number);

                    sb.Append(", PullRequestId={PullRequestId}");
                    args.Add(pullRequestEvent.PullRequest.Id);
                }

                if (message is LabelEvent labelEvent)
                {
                    sb.Append(", Label={Label}");
                    args.Add(labelEvent.Label.Name);

                    sb.Append(", LabelId={LabelId}");
                    args.Add(labelEvent.Label.Id);
                }

                if (message is MilestoneEvent milestoneEvent)
                {
                    sb.Append(", Milestone={Milestone}");
                    args.Add(milestoneEvent.Milestone.Title);

                    sb.Append(", MilestoneId={MilestoneId}");
                    args.Add(milestoneEvent.Milestone.Id);
                }

                if (message is IssueCommentEvent issueCommentEvent)
                {
                    sb.Append(", Comment={Comment}");
                    args.Add(issueCommentEvent.Comment.Id);
                }

                if (message.Sender is not null)
                {
                    sb.Append(", Sender={Sender}");
                    args.Add(message.Sender.Login);

                    sb.Append(", SenderId={SenderId}");
                    args.Add(message.Sender.Id);
                }

                if (message.Installation is not null)
                {
                    sb.Append(", Installation={Installation}");
                    args.Add(message.Installation.Id);
                }
            }
        }

        protected override Task ProcessRepositoryWebhookAsync(WebhookHeaders headers, RepositoryEvent repositoryEvent, RepositoryAction action)
        {
            switch (action)
            {
                case RepositoryActionValue.Created:
                    AddRepo(repositoryEvent.Repository);
                    break;
                case RepositoryActionValue.Deleted:
                    RemoveRepo(repositoryEvent.Repository);
                    break;
                case RepositoryActionValue.Archived:
                case RepositoryActionValue.Unarchived:
                case RepositoryActionValue.Publicized:
                case RepositoryActionValue.Privatized:
                    UpdateRepo(repositoryEvent.Repository);
                    break;
            }

            return base.ProcessRepositoryWebhookAsync(headers, repositoryEvent, action);
        }

        protected override Task ProcessLabelWebhookAsync(WebhookHeaders headers, LabelEvent labelEvent, LabelAction action)
        {
            switch (action)
            {
                case LabelActionValue.Created:
                    AddLabel(labelEvent.Repository, labelEvent.Label);
                    break;
                case LabelActionValue.Edited:
                    UpdateLabel(labelEvent.Repository, labelEvent.Label);
                    break;
                case LabelActionValue.Deleted:
                    RemoveLabel(labelEvent.Repository, labelEvent.Label);
                    break;
            }


            return base.ProcessLabelWebhookAsync(headers, labelEvent, action);
        }

        protected override Task ProcessMilestoneWebhookAsync(WebhookHeaders headers, MilestoneEvent milestoneEvent, MilestoneAction action)
        {
            switch (action)
            {
                case MilestoneActionValue.Created:
                    AddMilestone(milestoneEvent.Repository, milestoneEvent.Milestone);
                    break;
                case MilestoneActionValue.Edited:
                case MilestoneActionValue.Opened:
                case MilestoneActionValue.Closed:
                    UpdateMilestone(milestoneEvent.Repository, milestoneEvent.Milestone);
                    break;
                case MilestoneActionValue.Deleted:
                    RemoveMilestone(milestoneEvent.Repository, milestoneEvent.Milestone);
                    break;
            }

            return base.ProcessMilestoneWebhookAsync(headers, milestoneEvent, action);
        }

        protected override Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent issueEvent, IssuesAction action)
        {
            switch (action)
            {
                case IssuesActionValue.Opened:
                    AddIssue(issueEvent.Repository, issueEvent.Issue);
                    break;
                case IssuesActionValue.Closed:
                case IssuesActionValue.Reopened:
                case IssuesActionValue.Edited:
                case IssuesActionValue.Assigned:
                case IssuesActionValue.Unassigned:
                case IssuesActionValue.Labeled:
                case IssuesActionValue.Unlabeled:
                case IssuesActionValue.Milestoned:
                case IssuesActionValue.Demilestoned:
                case IssuesActionValue.Locked:
                case IssuesActionValue.Unlocked:
                    UpdateIssue(issueEvent.Repository, issueEvent.Issue);
                    break;
                case IssuesActionValue.Deleted:
                    RemoveIssue(issueEvent.Repository, issueEvent.Issue);
                    break;
                case IssuesActionValue.Transferred:
                    TransferIssue(issueEvent.Repository, issueEvent.Issue);
                    break;
            }

            return base.ProcessIssuesWebhookAsync(headers, issueEvent, action);
        }

        protected override Task ProcessPullRequestWebhookAsync(WebhookHeaders headers, PullRequestEvent pullRequestEvent, PullRequestAction action)
        {
            switch (action)
            {
                case PullRequestActionValue.Opened:
                    AddPullRequest(pullRequestEvent.Repository, pullRequestEvent.PullRequest);
                    break;
                case PullRequestActionValue.Closed:
                case PullRequestActionValue.Reopened:
                case PullRequestActionValue.Edited:
                case PullRequestActionValue.Assigned:
                case PullRequestActionValue.Unassigned:
                case PullRequestActionValue.Labeled:
                case PullRequestActionValue.Unlabeled:
                case PullRequestActionValue.Locked:
                case PullRequestActionValue.Unlocked:
                case PullRequestActionValue.ConvertedToDraft:
                case PullRequestActionValue.ReadyForReview:
                    UpdatePullRequest(pullRequestEvent.Repository, pullRequestEvent.PullRequest);
                    break;
            }

            return base.ProcessPullRequestWebhookAsync(headers, pullRequestEvent, action);
        }

        private void AddRepo(Repository repository)
        {
            if (repository.Private)
                return;

            var index = _indexService.Index;
            if (index is null)
                return;

            var crawledRepo = new CrawledRepo();
            crawledRepo.Id = repository.Id;
            crawledRepo.Size = repository.Size;

            UpdateRepo(repository, crawledRepo);

            index.Repos = index.Repos.CopyAndAdd(crawledRepo);

            _indexService.NotifyIndexChanged();
        }

        private void UpdateRepo(Repository repository)
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

        private static void UpdateRepo(Repository repository, CrawledRepo crawledRepo)
        {
            crawledRepo.Org = repository.Owner.Login;
            crawledRepo.Name = repository.Name;
            crawledRepo.IsArchived = repository.Archived;
        }

        private void RemoveRepo(Repository repository)
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

        private void AddLabel(Repository repository, Label label)
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

        private static CrawledLabel CreateLabel(CrawledRepo crawledRepo, Label label)
        {
            var crawledLabel = new CrawledLabel();
            crawledLabel.Id = label.Id;
            UpdateLabel(label, crawledLabel);

            crawledRepo.Labels = crawledRepo.Labels.CopyAndAdd(crawledLabel);

            return crawledLabel;
        }

        private void UpdateLabel(Repository repository, Label label)
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

        private static void UpdateLabel(Label label, CrawledLabel crawledLabel)
        {
            crawledLabel.ColorText = label.Color;
            crawledLabel.Description = label.Description;
            crawledLabel.Name = label.Name;
        }

        private void RemoveLabel(Repository repository, Label label)
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

        private CrawledLabel GetOrCreateLabel(Repository repository, Label label)
        {
            var index = _indexService.Index;
            Debug.Assert(index is not null);

            var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
            Debug.Assert(crawledRepo is not null);

            var matchingLabels = crawledRepo.Labels.Where(l => l.Id == label.Id).ToArray();
            if (matchingLabels.Length == 0)
                return CreateLabel(crawledRepo, label);

            if (matchingLabels.Length > 1)
            {
                var label1 = matchingLabels[0];
                var label2 = matchingLabels[1];
                _logger.LogError("In repo {org}/{repo} multiple labels have id {labelId}: '{label1Name}', '{label2Name}'", crawledRepo.Org, crawledRepo.Name, label.Id, label1.Name, label2.Name);
            }

            return matchingLabels[0];
        }

        private void AddMilestone(Repository repository, Milestone milestone)
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

        private static CrawledMilestone CreateMilestone(CrawledRepo crawledRepo, Milestone milestone)
        {
            var crawledMilestone = new CrawledMilestone();
            crawledMilestone.Id = milestone.Id;
            crawledMilestone.Number = milestone.Number;
            UpdateMilestone(milestone, crawledMilestone);

            crawledRepo.Milestones = crawledRepo.Milestones.CopyAndAdd(crawledMilestone);

            return crawledMilestone;
        }

        private void UpdateMilestone(Repository repository, Milestone milestone)
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

        private static void UpdateMilestone(Milestone milestone, CrawledMilestone crawledMilestone)
        {
            crawledMilestone.Title = milestone.Title;
            crawledMilestone.Description = milestone.Description;
        }

        private void RemoveMilestone(Repository repository, Milestone milestone)
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

        private CrawledMilestone GetOrCreateMilestone(Repository repository, Milestone milestone)
        {
            if (milestone is null)
                return null;

            var index = _indexService.Index;
            Debug.Assert(index is not null);

            var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
            Debug.Assert(crawledRepo is not null);

            var matchingMilestones = crawledRepo.Milestones.Where(m => m.Id == milestone.Id).ToArray();
            if (matchingMilestones.Length == 0)
                return CreateMilestone(crawledRepo, milestone);

            if (matchingMilestones.Length > 1)
            {
                var milestone1 = matchingMilestones[0];
                var milestone2 = matchingMilestones[1];
                _logger.LogError("In repo {org}/{repo} multiple milestones have id {milestoneId}: '{milestone1Name}', '{milestone2Name}'", crawledRepo.Org, crawledRepo.Name, milestone.Id, milestone1.Title, milestone2.Title);
            }

            return matchingMilestones[0];
        }

        private void AddIssue(Repository repository, Issue issue)
        {
            var index = _indexService.Index;
            if (index is null)
                return;

            var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
            if (crawledRepo is null)
                return;

            var crawledIssue = new CrawledIssue();
            crawledIssue.Id = issue.Id;
            crawledIssue.Repo = crawledRepo;
            crawledIssue.Number = issue.Number;
            crawledIssue.CreatedAt = issue.CreatedAt;
            crawledIssue.CreatedBy = issue.User.Login;
            crawledIssue.IsPullRequest = false;

            UpdateIssue(repository, issue, crawledIssue);

            crawledRepo.Issues[crawledIssue.Number] = crawledIssue;

            AddTrieTerms(crawledIssue, crawledIssue.GetTrieTerms());

            _indexService.NotifyIndexChanged();
        }

        private void AddPullRequest(Repository repository, PullRequest pullRequest)
        {
            var index = _indexService.Index;
            if (index is null)
                return;

            var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
            if (crawledRepo is null)
                return;

            var crawledIssue = new CrawledIssue();
            crawledIssue.Id = pullRequest.Id;
            crawledIssue.Repo = crawledRepo;
            crawledIssue.Number = pullRequest.Number;
            crawledIssue.CreatedAt = pullRequest.CreatedAt;
            crawledIssue.CreatedBy = pullRequest.User.Login;
            crawledIssue.IsPullRequest = true;

            UpdatePullRequest(repository, pullRequest, crawledIssue);

            crawledRepo.Issues[crawledIssue.Number] = crawledIssue;

            AddTrieTerms(crawledIssue, crawledIssue.GetTrieTerms());

            _indexService.NotifyIndexChanged();
        }

        private void UpdateIssue(Repository repository, Issue issue)
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

            UpdateIssue(repository, issue, crawledIssue);
            UpdateTrie(crawledIssue, oldTrieTerms);

            _indexService.NotifyIndexChanged();
        }

        private void UpdateIssue(Repository repository, Issue issue, CrawledIssue crawledIssue)
        {
            crawledIssue.IsOpen = issue.State == "open";
            crawledIssue.Title = issue.Title;
            crawledIssue.IsLocked = issue.Locked == true;
            crawledIssue.UpdatedAt = issue.UpdatedAt; // TODO: Non-atomic write
            crawledIssue.ClosedAt = issue.ClosedAt;   // TODO: Non-atomic write
            crawledIssue.Assignees = issue.Assignees.Select(a => a.Login).ToArray();
            crawledIssue.Labels = issue.Labels.Select(l => GetOrCreateLabel(repository, l)).ToArray();
            crawledIssue.Milestone = GetOrCreateMilestone(repository, issue.Milestone);
        }

        private void UpdatePullRequest(Repository repository, PullRequest pullRequest, CrawledIssue crawledIssue)
        {
            crawledIssue.IsOpen = pullRequest.State == "open";
            crawledIssue.Title = pullRequest.Title;
            crawledIssue.IsLocked = pullRequest.Locked == true;
            crawledIssue.UpdatedAt = pullRequest.UpdatedAt; // TODO: Non-atomic write
            crawledIssue.ClosedAt = pullRequest.ClosedAt;   // TODO: Non-atomic write
            crawledIssue.Assignees = pullRequest.Assignees.Select(a => a.Login).ToArray();
            crawledIssue.Labels = pullRequest.Labels.Select(l => GetOrCreateLabel(repository, l)).ToArray();
            crawledIssue.Milestone = GetOrCreateMilestone(repository, pullRequest.Milestone);
            crawledIssue.IsMerged = pullRequest.Merged == true;
            crawledIssue.IsDraft = pullRequest.Draft;
        }

        private void TransferIssue(Repository repository, Issue issue)
        {
            // When an issue is being transferred, GitHub sends two events:
            //
            // 1. Issue transferred (existing repo, existing issue, new repo, new issue)
            // 2. Issue opened (new repo, new issue)
            //
            // The existing issue in event (1) isn't marked as closed yet, but we also don't get
            // a dedicated "issue closed" event either.
            //
            // Hence, handling a transfer only requires us to remove the existing issue. We can
            // ignore the new repo and new issue because we'll get a dedicated "isse opened"
            // event anyways.

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

        private void RemoveIssue(Repository repository, Issue issue)
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

        private void UpdatePullRequest(Repository repository, PullRequest pullRequest)
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

            UpdatePullRequest(repository, pullRequest, crawledIssue);
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
