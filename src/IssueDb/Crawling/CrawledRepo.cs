﻿using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssueDb.Crawling;

// TODO: We may want to support private repos
//
//       If we do, we probably need to store the set of users that have access to that repo
//       so that we can efficiently do access control checks.
//
//       Hopefully, GitHub apps have a hook that lets us know when access permissions for
//       a repo have changed.

public sealed class CrawledRepo
{
    public long Id { get; set; }
    public string Org { get; set; }
    public string Name { get; set; }
    public bool IsArchived { get; set; }
    public long Size { get; set; }
    public Dictionary<int, CrawledIssue> Issues { get; set; } = new();
    public List<CrawledLabel> Labels { get; set; } = new();
    public List<CrawledMilestone> Milestones { get; set; } = new();

    public DateTimeOffset? LastReindex { get; set; }

    [JsonIgnore]
    public AreaOwnership AreaOwnership { get; set; } = AreaOwnership.Empty;

    [JsonIgnore]
    public string FullName => $"{Org}/{Name}";

    [JsonIgnore]
    public DateTimeOffset? IncrementalUpdateStart => LastReindex is null || !Issues.Any() ? null : Issues.Values.Max(i => i.UpdatedAt ?? i.CreatedAt);

    public void Clear()
    {
        IsArchived = false;
        Size = 0;
        Issues = new();
        Labels = new();
        Milestones = new();
        LastReindex = null;
        AreaOwnership = AreaOwnership.Empty;
    }

    public static async Task<CrawledRepo> LoadAsync(string path)
    {
        if (!File.Exists(path))
            return null;

        using (var fileStream = File.OpenRead(path))
        using (var deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            return await JsonSerializer.DeserializeAsync<CrawledRepo>(deflateStream, options);
        }
    }

    public async Task SaveAsync(string path)
    {
        using (var fileStream = File.Create(path))
        using (var deflateStream = new DeflateStream(fileStream, CompressionLevel.Optimal))
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            await JsonSerializer.SerializeAsync(deflateStream, this, options);
        }
    }
}
