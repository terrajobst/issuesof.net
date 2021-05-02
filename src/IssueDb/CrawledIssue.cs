using System;
using System.Text.Json.Serialization;

namespace IssuesOfDotNet
{
    // TODO: We should record whether an issue was deleted or transferred
    //
    //       Maybe we can use the GraphQL APIs to make this more palatable to query,
    //       rather than having to fetch the events for every single issue.
    //
    //       But before we do any of that, we should create a temp repo and see
    //       what actually happens.
    //
    // TODO: We should pre-process the title's markdown into HTML (for faster display)

    public sealed class CrawledIssue
    {
        public string Org { get; set; }
        public string Repo { get; set; }
        public CrawledIssueState State { get; set; }
        public int Number { get; set; }
        public bool IsPullRequest { get; set; }
        public bool IsDraft { get; set; }
        public bool IsMerged { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }
        public string CreatedBy { get; set; }
        // TODO: Seems this is always null. Should we strip that?
        public string ClosedBy { get; set; }
        public string[] Assignees { get; set; }
        public CrawledLabel[] Labels { get; set; }
        public CrawledMilestone Milestone { get; set; }

        [JsonIgnore]
        public string Url => IsPullRequest
                                ? $"https://github.com/{Org}/{Repo}/issues/{Number}"
                                : $"https://github.com/{Org}/{Repo}/pull/{Number}";
    }
}
