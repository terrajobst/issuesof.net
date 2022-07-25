using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IssueDb.Crawling
{
    public sealed class CrawledIndex
    {
        private static readonly byte[] _formatMagicNumbers = new byte[] { (byte)'G', (byte)'H', (byte)'C', (byte)'T' };
        private static readonly short _formatVersion = 8;

        public List<CrawledRepo> Repos { get; set; } = new();

        public CrawledTrie<CrawledIssue> Trie { get; set; } = new();

        public async Task SaveAsync(string path)
        {
            using (var fileStream = File.Create(path))
            {
                var stringIndex = new Dictionary<string, int>(StringComparer.Ordinal);

                var stringIndexer = new Func<string, int>(s =>
                {
                    if (s is null)
                        return -1;

                    if (!stringIndex.TryGetValue(s, out var index))
                    {
                        index = stringIndex.Count;
                        stringIndex.Add(s, index);
                    }

                    return index;
                });

                using (var memoryStream = new MemoryStream())
                {
                    // Write repos and nodes into memory so that we have a complete string index.

                    using (var memoryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
                    {
                        var issueIndex = new Dictionary<CrawledIssue, int>();

                        WriteRepos(memoryWriter, Repos, stringIndexer, issueIndex);
                        WriteNode(memoryWriter, Trie.Root, stringIndexer, issueIndex);
                    }

                    // Write header, uncompressed

                    using (var writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true))
                    {
                        // Write header

                        writer.Write(_formatMagicNumbers);
                        writer.Write(_formatVersion);
                    }

                    // Write strings and buffered repos and nodes, compressed

                    using (var deflateStream = new DeflateStream(fileStream, CompressionLevel.Optimal))
                    {
                        // Write strings

                        using (var writer = new BinaryWriter(deflateStream, Encoding.UTF8, leaveOpen: true))
                        {
                            writer.Write(stringIndex.Count);
                            foreach (var str in stringIndex.OrderBy(kv => kv.Value).Select(kv => kv.Key))
                                writer.Write(str);
                        }

                        // Write buffered repos and nodes

                        memoryStream.Position = 0;
                        await memoryStream.CopyToAsync(deflateStream);
                    }
                }
            }

            static void WriteRepos(BinaryWriter writer,
                                   IReadOnlyList<CrawledRepo> repos,
                                   Func<string, int> stringIndexer,
                                   Dictionary<CrawledIssue, int> issueIndex)
            {
                writer.Write(repos.Count);

                foreach (var repo in repos)
                {
                    writer.Write(repo.Id);
                    writer.Write(stringIndexer(repo.Org));
                    writer.Write(stringIndexer(repo.Name));
                    writer.Write(repo.IsArchived);
                    writer.Write(repo.Size);
                    writer.Write(repo.LastReindex?.UtcTicks ?? -1);

                    // Write labels

                    var labelIndex = new Dictionary<CrawledLabel, int>();

                    writer.Write(repo.Labels.Count);

                    foreach (var label in repo.Labels)
                    {
                        labelIndex.Add(label, labelIndex.Count);

                        writer.Write(label.Id);
                        writer.Write(stringIndexer(label.Name));
                        writer.Write(stringIndexer(label.Description));
                        writer.Write(stringIndexer(label.ColorText));
                    }

                    // Write milestones

                    var milestoneIndex = new Dictionary<CrawledMilestone, int>();

                    writer.Write(repo.Milestones.Count);

                    foreach (var milestone in repo.Milestones)
                    {
                        milestoneIndex.Add(milestone, milestoneIndex.Count);

                        writer.Write(milestone.Id);
                        writer.Write(milestone.Number);
                        writer.Write(stringIndexer(milestone.Title));
                        writer.Write(stringIndexer(milestone.Description));
                    }

                    // Write issues

                    writer.Write(repo.Issues.Count);

                    foreach (var issue in repo.Issues.Values)
                    {
                        var issueId = issueIndex.Count;
                        issueIndex.Add(issue, issueId);

                        writer.Write(issueId);
                        // Org  : Ignored, because it's implied by the containing repo.
                        // Repo : Ignored, because it's implied by the containing repo.
                        writer.Write(issue.Id);
                        writer.Write(issue.Number);
                        writer.Write(issue.IsOpen);
                        writer.Write(issue.IsPullRequest);
                        writer.Write(issue.IsDraft);
                        writer.Write(issue.IsMerged);
                        writer.Write(stringIndexer(issue.Title));
                        // Body : Ignored because we don't need anymore.
                        writer.Write(issue.CreatedAt.UtcTicks);
                        writer.Write(issue.UpdatedAt?.UtcTicks ?? -1);
                        writer.Write(issue.ClosedAt?.UtcTicks ?? -1);
                        writer.Write(stringIndexer(issue.CreatedBy));
                        writer.Write(issue.IsLocked);
                        writer.Write(issue.Comments);
                        writer.Write(issue.ReactionsPlus1);
                        writer.Write(issue.ReactionsMinus1);
                        writer.Write(issue.ReactionsSmile);
                        writer.Write(issue.ReactionsTada);
                        writer.Write(issue.ReactionsThinkingFace);
                        writer.Write(issue.ReactionsHeart);

                        writer.Write(issue.Assignees.Length);
                        foreach (var assignee in issue.Assignees)
                            writer.Write(stringIndexer(assignee));

                        writer.Write(issue.Labels.Length);
                        foreach (var label in issue.Labels)
                            writer.Write(labelIndex[label]);

                        if (issue.Milestone is null)
                            writer.Write(-1);
                        else
                            writer.Write(milestoneIndex[issue.Milestone]);
                    }

                    // Write area owners

                    if (repo.AreaOwners is null)
                    {
                        writer.Write(0);
                    }
                    else
                    {
                        writer.Write(repo.AreaOwners.Count);

                        foreach (var kv in repo.AreaOwners)
                        {
                            writer.Write(stringIndexer(kv.Key));
                            writer.Write(stringIndexer(kv.Value.Lead));
                            writer.Write(stringIndexer(kv.Value.Pod));
                            writer.Write(kv.Value.Owners.Count);
                            foreach (var owner in kv.Value.Owners)
                                writer.Write(stringIndexer(owner));
                        }
                    }
                }
            }

            static void WriteNode(BinaryWriter writer,
                                  CrawledTrieNode<CrawledIssue> node,
                                  Func<string, int> stringIndexer,
                                  Dictionary<CrawledIssue, int> issueIndex)
            {
                writer.Write(stringIndexer(node.Text));

                writer.Write(node.Values.Length);
                foreach (var issue in node.Values)
                    writer.Write(issueIndex[issue]);

                writer.Write(node.Children.Length);
                foreach (var child in node.Children)
                    WriteNode(writer, child, stringIndexer, issueIndex);
            }
        }

        public static async Task<CrawledIndex> LoadAsync(string path)
        {
            using var stream = File.OpenRead(path);
            return await LoadAsync(stream);
        }

        public static Task<CrawledIndex> LoadAsync(Stream stream)
        {
            // Validate header

            if (stream.Length < 6)
                throw new InvalidDataException();

            var header = new byte[6];
            stream.Read(header);

            if (!header.AsSpan(0, 4).SequenceEqual(_formatMagicNumbers))
                throw new InvalidDataException();

            var formatVersion = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(4));
            if (formatVersion != _formatVersion)
                throw new InvalidDataException();

            // Read contents

            var issueIndex = new Dictionary<int, CrawledIssue>();

            using (var deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(deflateStream, Encoding.UTF8))
            {
                // Read strings

                var stringCount = reader.ReadInt32();
                var stringIndex = new Dictionary<int, string>()
                {
                    { -1, null }
                };

                for (var i = 0; i < stringCount; i++)
                {
                    var s = reader.ReadString();
                    stringIndex.Add(i, s);
                }

                // Read repos

                var repoCount = reader.ReadInt32();
                var repos = new List<CrawledRepo>(repoCount);

                for (var i = 0; i < repoCount; i++)
                {
                    var repoId = reader.ReadInt64();
                    var org = stringIndex[reader.ReadInt32()];
                    var name = stringIndex[reader.ReadInt32()];
                    var isArchived = reader.ReadBoolean();
                    var size = reader.ReadInt64();
                    var lastReindex = ToNullableDateTime(reader.ReadInt64());

                    var repo = new CrawledRepo()
                    {
                        Id = repoId,
                        Org = org,
                        Name = name,
                        IsArchived = isArchived,
                        Size = size,
                        LastReindex = lastReindex
                    };
                    repos.Add(repo);

                    // Read labels

                    var labelCount = reader.ReadInt32();
                    var labelIndex = new Dictionary<int, CrawledLabel>();

                    for (var labelId = 0; labelId < labelCount; labelId++)
                    {
                        var label = new CrawledLabel
                        {
                            Id = reader.ReadInt64(),
                            Name = stringIndex[reader.ReadInt32()],
                            Description = stringIndex[reader.ReadInt32()],
                            ColorText = stringIndex[reader.ReadInt32()]
                        };
                        labelIndex.Add(labelId, label);
                        repo.Labels = repo.Labels.CopyAndAdd(label);
                    }

                    // Read milestones

                    var milestoneCount = reader.ReadInt32();
                    var milestoneIndex = new Dictionary<int, CrawledMilestone>
                    {
                        { -1, null }
                    };

                    for (var milestoneId = 0; milestoneId < milestoneCount; milestoneId++)
                    {
                        var milestone = new CrawledMilestone
                        {
                            Id = reader.ReadInt64(),
                            Number = reader.ReadInt32(),
                            Title = stringIndex[reader.ReadInt32()],
                            Description = stringIndex[reader.ReadInt32()],
                        };
                        milestoneIndex.Add(milestoneId, milestone);
                        repo.Milestones = repo.Milestones.CopyAndAdd(milestone);
                    }

                    // Read issues

                    var issueCount = reader.ReadInt32();

                    while (issueCount-- > 0)
                    {
                        var issueId = reader.ReadInt32();

                        var issue = new CrawledIssue
                        {
                            Repo = repo,
                            Id = reader.ReadInt64(),
                            Number = reader.ReadInt32(),
                            IsOpen = reader.ReadBoolean(),
                            IsPullRequest = reader.ReadBoolean(),
                            IsDraft = reader.ReadBoolean(),
                            IsMerged = reader.ReadBoolean(),
                            Title = stringIndex[reader.ReadInt32()],
                            // Body : ignored because we don't need here
                            CreatedAt = new DateTime(reader.ReadInt64()),
                            UpdatedAt = ToNullableDateTime(reader.ReadInt64()),
                            ClosedAt = ToNullableDateTime(reader.ReadInt64()),
                            CreatedBy = stringIndex[reader.ReadInt32()],
                            IsLocked = reader.ReadBoolean(),
                            Comments = reader.ReadInt32(),
                            ReactionsPlus1 = reader.ReadInt32(),
                            ReactionsMinus1 = reader.ReadInt32(),
                            ReactionsSmile = reader.ReadInt32(),
                            ReactionsTada = reader.ReadInt32(),
                            ReactionsThinkingFace = reader.ReadInt32(),
                            ReactionsHeart = reader.ReadInt32()
                        };

                        var assigneeCount = reader.ReadInt32();
                        var assignees = new List<string>(assigneeCount);
                        while (assigneeCount-- > 0)
                            assignees.Add(stringIndex[reader.ReadInt32()]);
                        issue.Assignees = assignees.ToArray();

                        var assignedLabelCount = reader.ReadInt32();
                        var labels = new List<CrawledLabel>(assignedLabelCount);
                        while (assignedLabelCount-- > 0)
                            labels.Add(labelIndex[reader.ReadInt32()]);
                        issue.Labels = labels.ToArray();

                        issue.Milestone = milestoneIndex[reader.ReadInt32()];

                        repo.Issues.Add(issue.Number, issue);
                        issueIndex.Add(issueId, issue);
                    }

                    // Read area owners

                    var areaEntryCount = reader.ReadInt32();

                    repo.AreaOwners = new Dictionary<string, CrawledAreaOwnerEntry>(StringComparer.OrdinalIgnoreCase);

                    while (areaEntryCount-- > 0)
                    {
                        var area = stringIndex[reader.ReadInt32()];
                        var lead = stringIndex[reader.ReadInt32()];
                        var pod = stringIndex[reader.ReadInt32()];
                        var ownerCount = reader.ReadInt32();
                        var owners = new List<string>(ownerCount);
                        while (ownerCount-- > 0)
                        {
                            var owner = stringIndex[reader.ReadInt32()];
                            owners.Add(owner);
                        }

                        repo.AreaOwners[area] = new CrawledAreaOwnerEntry(area, lead, pod, owners.ToArray());
                    }
                }

                var root = ReadNode(reader, stringIndex, issueIndex);

                return Task.FromResult(new CrawledIndex
                {
                    Repos = repos.ToList(),
                    Trie = new CrawledTrie<CrawledIssue>(root)
                });
            }

            static DateTimeOffset? ToNullableDateTime(long ticks)
            {
                if (ticks == -1)
                    return null;

                return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)).ToLocalTime();
            }

            static CrawledTrieNode<CrawledIssue> ReadNode(BinaryReader reader,
                                                          Dictionary<int, string> stringIndex,
                                                          Dictionary<int, CrawledIssue> issueIndex)
            {
                var text = stringIndex[reader.ReadInt32()];

                var issueCount = reader.ReadInt32();
                var issues = new List<CrawledIssue>(issueCount);
                while (issueCount-- > 0)
                {
                    var issueId = reader.ReadInt32();
                    issues.Add(issueIndex[issueId]);
                }

                var childrenCount = reader.ReadInt32();
                var children = new List<CrawledTrieNode<CrawledIssue>>(childrenCount);
                while (childrenCount-- > 0)
                {
                    var node = ReadNode(reader, stringIndex, issueIndex);
                    children.Add(node);
                }

                return new CrawledTrieNode<CrawledIssue>(text, children, issues);
            }
        }
    }
}
