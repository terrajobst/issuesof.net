using System;

namespace IssuesOfDotNet.net.Data
{
    public class RepoStats
    {
        public string Org { get; set; }
        public string Repo { get; set; }
        public DateTimeOffset? LastUpdatedAt { get; set; }
        public int NumberOfIssues { get; set; }
    }
}
