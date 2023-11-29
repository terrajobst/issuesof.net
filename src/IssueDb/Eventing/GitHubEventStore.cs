using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Azure.Storage.Blobs;

namespace IssueDb.Eventing;

public sealed class GitHubEventStore
{
    private readonly string _connectionString;

    public GitHubEventStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    private BlobContainerClient GetClient()
    {
        return new BlobContainerClient(_connectionString, "events");
    }

    public async Task SaveAsync(GitHubEventPayloadName name, GitHubEventPayload payload)
    {
        var blobName = name.ToString();
        var blobContent = payload.ToJson();

        var client = GetClient();

        await client.CreateIfNotExistsAsync();

        using (var memoryStream = new MemoryStream())
        {
            using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                writer.Write(blobContent);

            memoryStream.Position = 0;

            await client.UploadBlobAsync(blobName, memoryStream);
        }
    }

    public async Task<IReadOnlyList<GitHubEventPayloadName>> ListAsync()
    {
        var result = new List<GitHubEventPayloadName>();

        var client = GetClient();
        await foreach (var blob in client.GetBlobsAsync())
        {
            var name = blob.Name;
            var payloadName = GitHubEventPayloadName.Parse(name);
            result.Add(payloadName);
        }

        result.Sort();

        return result.ToArray();
    }

    public async Task<GitHubEventPayload> LoadAsync(GitHubEventPayloadName name)
    {
        var containerClient = GetClient();
        var blobName = name.ToString();
        var blobClient = new BlobClient(_connectionString, containerClient.Name, blobName);
        var data = await blobClient.DownloadAsync();
        
        using (var reader = new StreamReader(data.Value.Content))
        {
            var json = await reader.ReadToEndAsync();
            return GitHubEventPayload.ParseJson(json);
        }
    }

    public async Task DeleteAsync(GitHubEventPayloadName name)
    {
        var blobName = name.ToString();
        var client = GetClient();
        await client.DeleteBlobAsync(blobName);
    }

    public async Task DeleteAsync(IEnumerable<GitHubEventPayloadName> names)
    {
        var client = GetClient();

        foreach (var name in names)
        {
            var blobName = name.ToString();
            await client.DeleteBlobAsync(blobName);
        }
    }
}
