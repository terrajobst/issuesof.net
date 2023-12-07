using System.Diagnostics;
using System.Text;

using IssueDb;
using IssueDb.Crawling;

using Terrajobst.GitHubEvents;

namespace IssuesOfDotNet.Data;

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
            {
                var sb = new StringBuilder();
                var args = new List<object>();

                sb.Append("Processing message");

                FormatMessage(message, sb, args);

                _logger.LogInformation(sb.ToString(), args.ToArray());
            }

            try
            {
                base.ProcessMessage(message);
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                var args = new List<object>();

                sb.Append("Error processing message");

                FormatMessage(message, sb, args);

                _logger.LogError(ex, sb.ToString(), args.ToArray());
            }

            static void FormatMessage(GitHubEventMessage message, StringBuilder sb, List<object> args)
            {
                sb.Append(", Delivery={Delivery}");
                args.Add(message.Headers.Delivery);

                if (message.Headers.Event is not null)
                {
                    sb.Append(", Event={Event}");
                    args.Add(message.Headers.Event);
                }

                if (message.Body.Action is not null)
                {
                    sb.Append(", Action={Action}");
                    args.Add(message.Body.Action);
                }

                if (message.Body.Organization is not null)
                {
                    sb.Append(", Org={Org}");
                    args.Add(message.Body.Organization.Login);

                    sb.Append(", OrgId={OrgId}");
                    args.Add(message.Body.Organization.Id);
                }

                if (message.Body.Repository is not null)
                {
                    sb.Append(", Repo={Repo}");
                    args.Add(message.Body.Repository.Name);

                    sb.Append(", RepoId={RepoId}");
                    args.Add(message.Body.Repository.Id);
                }

                if (message.Body.Issue is not null)
                {
                    sb.Append(", Issue={Issue}");
                    args.Add(message.Body.Issue.Number);

                    sb.Append(", IssueId={IssueId}");
                    args.Add(message.Body.Issue.Id);
                }

                if (message.Body.PullRequest is not null)
                {
                    sb.Append(", PullRequest={PullRequest}");
                    args.Add(message.Body.PullRequest.Number);

                    sb.Append(", PullRequestId={PullRequestId}");
                    args.Add(message.Body.PullRequest.Id);
                }

                if (message.Body.Label is not null)
                {
                    sb.Append(", Label={Label}");
                    args.Add(message.Body.Label.Name);

                    sb.Append(", LabelId={LabelId}");
                    args.Add(message.Body.Label.Id);
                }

                if (message.Body.Milestone is not null)
                {
                    sb.Append(", Milestone={Milestone}");
                    args.Add(message.Body.Milestone.Title);

                    sb.Append(", MilestoneId={MilestoneId}");
                    args.Add(message.Body.Milestone.Id);
                }

                if (message.Body.Assignee is not null)
                {
                    sb.Append(", Assignee={Assignee}");
                    args.Add(message.Body.Assignee.Login);

                    sb.Append(", AssigneeId={AssigneeId}");
                    args.Add(message.Body.Assignee.Id);
                }

                if (message.Body.Comment is not null)
                {
                    sb.Append(", Comment={Comment}");
                    args.Add(message.Body.Comment.Id);
                }

                if (message.Body.Sender is not null)
                {
                    sb.Append(", Sender={Sender}");
                    args.Add(message.Body.Sender.Login);

                    sb.Append(", SenderId={SenderId}");
                    args.Add(message.Body.Sender.Id);
                }

                if (message.Body.Installation is not null)
                {
                    sb.Append(", Installation={Installation}");
                    args.Add(message.Body.Installation.Id);
                }
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
                    TransferIssue(repository, issue);
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
            crawledRepo.Id = repository.Id;
            crawledRepo.Size = repository.Size;

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
            crawledLabel.Id = label.Id;
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
            crawledMilestone.Id = milestone.Id;
            crawledMilestone.Number = milestone.Number;
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

        private CrawledMilestone? GetOrCreateMilestone(GitHubEventRepository repository, GitHubEventMilestone milestone)
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

        private void AddIssueOrPullRequest(GitHubEventRepository repository, GitHubEventIssueOrPullRequest issueOrPullRequest)
        {
            var index = _indexService.Index;
            if (index is null)
                return;

            var crawledRepo = index.Repos.SingleOrDefault(r => r.Id == repository.Id);
            if (crawledRepo is null)
                return;

            var crawledIssue = new CrawledIssue();
            crawledIssue.Id = issueOrPullRequest.Id;
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

        private void TransferIssue(GitHubEventRepository repository, GitHubEventIssue issue)
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
