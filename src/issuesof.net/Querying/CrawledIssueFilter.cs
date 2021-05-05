using System.Collections.Generic;

namespace IssuesOfDotNet.Querying
{
    public sealed class CrawledIssueFilter
    {
        public bool? IsOpen { get; set; }
        public bool? IsPullRequest { get; set; }
        public bool? IsMerged { get; set; }
        public bool? NoAssignees { get; set; }
        public bool? NoLabels { get; set; }
        public bool? NoMilestone { get; set; }

        public string Author { get; set; }
        public string Milestone { get; set; }

        public List<string> IncludedOrgs { get; } = new List<string>();
        public List<string> IncludedRepos { get; } = new List<string>();
        public List<string> IncludedAssignees { get; } = new List<string>();
        public List<string> IncludedLabels { get; } = new List<string>();
        public List<string> IncludedTerms { get; } = new List<string>();

        public List<string> ExcludedOrgs { get; } = new List<string>();
        public List<string> ExcludedRepos { get; } = new List<string>();
        public List<string> ExcludedAssignees { get; } = new List<string>();
        public List<string> ExcludedAuthors { get; } = new List<string>();
        public List<string> ExcludedLabels { get; } = new List<string>();
        public List<string> ExcludedMilestones { get; } = new List<string>();
        public List<string> ExcludedTerms { get; } = new List<string>();
    }
}
