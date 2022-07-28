namespace IssueDb.Crawling
{
    public sealed class CrawledIssueGroup
    {
        public CrawledIssueGroup(string[] keys, CrawledIssueOrGroup[] children)
        {
            Keys = keys;
            UniqueId = string.Join(',', keys);
            Children = children;
        }

        public string[] Keys { get; }
        public string UniqueId { get; }
        public CrawledIssueOrGroup[] Children { get; }
    }
}
