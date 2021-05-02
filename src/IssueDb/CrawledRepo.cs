using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IssuesOfDotNet
{
    // TODO: We may want to support private repos
    //
    //       If we do, we probbly need to store the set of users that have access to that repo
    //       so that we can efficiently do access control checks.
    //
    //       Hopefully, GitHub apps have a hook that lets us know when access permissions for
    //       a repo have changed.

    public sealed class CrawledRepo
    {
        public string Org { get; set; }
        public string Name { get; set; }
        public bool IsArchived { get; set; }
        public Dictionary<int, CrawledIssue> Issues { get; set; } = new Dictionary<int, CrawledIssue>();
        public List<CrawledLabel> Labels { get; set; } = new();
        public List<CrawledMilestone> Milestones { get; set; } = new();

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
}
