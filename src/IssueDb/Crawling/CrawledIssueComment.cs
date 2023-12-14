
using System.IO.Compression;
using System.Text.Json;

namespace IssueDb.Crawling;

public sealed class CrawledIssueComment
{
    public int Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByAssociation { get; set; } = string.Empty;
    public int ReactionsPlus1 { get; set; }
    public int ReactionsMinus1 { get; set; }
    public int ReactionsSmile { get; set; }
    public int ReactionsTada { get; set; }
    public int ReactionsThinkingFace { get; set; }
    public int ReactionsHeart { get; set; }

    public static async Task<IReadOnlyList<CrawledIssueComment>> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return [];

        using var fileStream = File.OpenRead(path);
        using var deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress);
        return await JsonSerializer.DeserializeAsync<CrawledIssueComment[]>(deflateStream) ?? [];
    }

    public static async Task SaveAsync(string path, IReadOnlyList<CrawledIssueComment> comments)
    {
        using var fileStream = File.Create(path);
        using var deflateStream = new DeflateStream(fileStream, CompressionLevel.Optimal);
        await JsonSerializer.SerializeAsync(deflateStream, comments);
    }
}
