namespace IssueDb.Crawling
{
    public sealed class CrawledAreaOwnerJsonEntry
    {
        public string Label { get; }
        public string Lead { get; }
        public string[] Owners { get; }

        public CrawledAreaOwnerJsonEntry(string label, string lead, string[] owners)
        {
            Label = label;
            Lead = lead;
            Owners = owners;
        }
    }
}
