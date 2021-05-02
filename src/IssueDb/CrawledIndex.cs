using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IssuesOfDotNet
{
    public sealed class CrawledIndex
    {
        private static readonly byte[] _formatMagicNumbers = new byte[] { (byte)'G', (byte)'H', (byte)'C', (byte)'T' };
        private static readonly short _formatVersion = 1;

        public IReadOnlyList<CrawledRepo> Repos { get; set; } = Array.Empty<CrawledRepo>();

        public CrawledTrie Trie { get; set; } = new();

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
                    writer.Write(stringIndexer(repo.Org));
                    writer.Write(stringIndexer(repo.Name));
                    writer.Write(repo.IsArchived);

                    // Write labels

                    var labelIndex = new Dictionary<CrawledLabel, int>();

                    writer.Write(repo.Labels.Count);

                    foreach (var label in repo.Labels)
                    {
                        labelIndex.Add(label, labelIndex.Count);

                        writer.Write(stringIndexer(label.Name));
                        writer.Write(stringIndexer(label.Description));
                        writer.Write(stringIndexer(label.ForegroundColor));
                        writer.Write(stringIndexer(label.BackgroundColor));
                    }

                    // Write milestones

                    var milestoneIndex = new Dictionary<CrawledMilestone, int>();

                    writer.Write(repo.Milestones.Count);

                    foreach (var milestone in repo.Milestones)
                    {
                        milestoneIndex.Add(milestone, milestoneIndex.Count);

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
                        writer.Write((byte)issue.State);
                        writer.Write(issue.Number);
                        writer.Write(issue.IsPullRequest);
                        writer.Write(issue.IsDraft);
                        writer.Write(issue.IsMerged);
                        writer.Write(stringIndexer(issue.Title));
                        // Body : Ignored because we don't need anymore.
                        writer.Write(issue.CreatedAt.Ticks);
                        writer.Write(issue.UpdatedAt?.Ticks ?? -1);
                        writer.Write(issue.ClosedAt?.Ticks ?? -1);
                        writer.Write(stringIndexer(issue.CreatedBy));
                        writer.Write(stringIndexer(issue.ClosedBy));

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
                }
            }

            static void WriteNode(BinaryWriter writer,
                                  CrawledTrieNode node,
                                  Func<string, int> stringIndexer,
                                  Dictionary<CrawledIssue, int> issueIndex)
            {
                writer.Write(stringIndexer(node.Text));

                writer.Write(node.Issues.Count);
                foreach (var issue in node.Issues)
                    writer.Write(issueIndex[issue]);

                writer.Write(node.Children.Count);
                foreach (var child in node.Children)
                    WriteNode(writer, child, stringIndexer, issueIndex);
            }
        }

        public static Task<CrawledIndex> LoadAsync(string path)
        {
            using (var stream = File.OpenRead(path))
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
                        var org = stringIndex[reader.ReadInt32()];
                        var name = stringIndex[reader.ReadInt32()];
                        var isArchived = reader.ReadBoolean();

                        var repo = new CrawledRepo()
                        {
                            Org = org,
                            Name = name,
                            IsArchived = isArchived,
                        };
                        repos.Add(repo);

                        // Read labels

                        var labelCount = reader.ReadInt32();
                        var labelIndex = new Dictionary<int, CrawledLabel>();

                        for (var labelId = 0; labelId < labelCount; labelId++)
                        {
                            var label = new CrawledLabel
                            {
                                Name = stringIndex[reader.ReadInt32()],
                                Description = stringIndex[reader.ReadInt32()],
                                ForegroundColor = stringIndex[reader.ReadInt32()],
                                BackgroundColor = stringIndex[reader.ReadInt32()]
                            };
                            labelIndex.Add(labelId, label);
                            repo.Labels.Add(label);
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
                                Number = reader.ReadInt32(),
                                Title = stringIndex[reader.ReadInt32()],
                                Description = stringIndex[reader.ReadInt32()],
                            };
                            milestoneIndex.Add(milestoneId, milestone);
                            repo.Milestones.Add(milestone);
                        }

                        var issueCount = reader.ReadInt32();

                        while (issueCount-- > 0)
                        {
                            var issueId = reader.ReadInt32();

                            var issue = new CrawledIssue
                            {
                                Org = org,
                                Repo = name,
                                State = (CrawledIssueState)reader.ReadByte(),
                                Number = reader.ReadInt32(),
                                IsPullRequest = reader.ReadBoolean(),
                                IsDraft = reader.ReadBoolean(),
                                IsMerged = reader.ReadBoolean(),
                                Title = stringIndex[reader.ReadInt32()],
                                // Body : ignored because we don't need here
                                CreatedAt = new DateTime(reader.ReadInt64()),
                                UpdatedAt = ToNullableDateTime(reader.ReadInt64()),
                                ClosedAt = ToNullableDateTime(reader.ReadInt64()),
                                CreatedBy = stringIndex[reader.ReadInt32()],
                                ClosedBy = stringIndex[reader.ReadInt32()],
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
                    }

                    var root = ReadNode(reader, stringIndex, issueIndex);

                    return Task.FromResult(new CrawledIndex
                    {
                        Repos = repos.ToArray(),
                        Trie = new CrawledTrie(root)
                    });
                }
            }

            static DateTime? ToNullableDateTime(long ticks)
            {
                if (ticks == -1)
                    return null;

                return new DateTime(ticks);
            }

            static CrawledTrieNode ReadNode(BinaryReader reader,
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
                var children = new List<CrawledTrieNode>(childrenCount);
                while (childrenCount-- > 0)
                {
                    var node = ReadNode(reader, stringIndex, issueIndex);
                    children.Add(node);
                }

                return new CrawledTrieNode(text, children, issues);
            }
        }
    }
}
