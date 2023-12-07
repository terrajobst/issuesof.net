using System.Text.Json;

namespace IssueDb.Eventing;

public sealed class GitHubEventPayload
{
    public GitHubEventPayload(IReadOnlyDictionary<string, IReadOnlyList<string>> headers, string body)
    {
        Headers = headers;
        Body = body;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    public string Body { get; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static GitHubEventPayload ParseJson(string json)
    {
        return JsonSerializer.Deserialize<GitHubEventPayload>(json)!;
    }
}
