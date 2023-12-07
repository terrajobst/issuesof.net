namespace IssuesOfDotNet.net.Data;

public class RepoStats
{
    public required string Org { get; set; }
    public required string Repo { get; set; }
    public string FullName => $"{Org}/{Repo}";
    public DateTimeOffset? LastUpdatedAt { get; set; }
    public int NumberOfOpenIssues { get; internal set; }
    public int NumberOfIssues { get; set; }
    public long Size { get; set; }
}
