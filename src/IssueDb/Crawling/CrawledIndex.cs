using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace IssueDb.Crawling;

public sealed class CrawledIndex
{
    private static readonly byte[] _formatMagicNumbers = new byte[] { (byte)'G', (byte)'H', (byte)'C', (byte)'T' };
    private static readonly short _currentFormatVersion = 12;
    private static readonly short _minSupportedFormatVersion = 11;

    public int Version { get; set; } = _currentFormatVersion;

    public static int LatestVersion => _currentFormatVersion;

    public CrawledAreaOwnership AreaOwnership { get; set; } = CrawledAreaOwnership.Empty;

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

                    WriteAreaOwnership(memoryWriter, AreaOwnership, stringIndexer);
                    WriteRepos(memoryWriter, Repos, stringIndexer, issueIndex);
                    WriteNode(memoryWriter, Trie.Root, stringIndexer, issueIndex);
                }

                // Write header, uncompressed

                using (var writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true))
                {
                    // Write header

                    writer.Write(_formatMagicNumbers);
                    writer.Write(_currentFormatVersion);
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

        static void WriteAreaOwnership(BinaryWriter writer,
                                       CrawledAreaOwnership ownership,
                                       Func<string, int> stringIndexer)
        {
            writer.Write(ownership.Entries.Count);

            foreach (var entry in ownership.Entries)
                WriteAreaEntry(writer, entry, stringIndexer);
        }

        static void WriteAreaEntry(BinaryWriter writer,
                                   CrawledAreaEntry entry,
                                   Func<string, int> stringIndexer)
        {
            writer.Write(stringIndexer(entry.Label));
            writer.Write(stringIndexer(entry.Area));
            WriteAreaMembers(writer, entry.Leads, stringIndexer);
            WriteAreaMembers(writer, entry.Owners, stringIndexer);
        }

        static void WriteAreaMembers(BinaryWriter writer,
                                     IReadOnlyList<CrawledAreaMember> members,
                                     Func<string, int> stringIndexer)
        {
            writer.Write(members.Count);

            foreach (var member in members)
            {
                writer.Write(stringIndexer(member.UserName));
                WriteAreaMemberOrigin(writer, member.Origin, stringIndexer);
            }
        }

        static void WriteAreaMemberOrigin(BinaryWriter writer,
                                          CrawledAreaMemberOrigin origin,
                                          Func<string, int> stringIndexer)
        {
            switch (origin)
            {
                case CrawledAreaMemberOrigin.Composite c:
                    writer.Write(0);
                    writer.Write(c.Origins.Count);
                    foreach (var o in c.Origins)
                        WriteAreaMemberOrigin(writer, o, stringIndexer);
                    break;
                case CrawledAreaMemberOrigin.File f:
                    writer.Write(1);
                    writer.Write(stringIndexer(f.OrgName));
                    writer.Write(stringIndexer(f.RepoName));
                    writer.Write(stringIndexer(f.Path));
                    writer.Write(f.LineNumber);
                    break;
                case CrawledAreaMemberOrigin.Team t:
                    writer.Write(2);
                    writer.Write(stringIndexer(t.OrgName));
                    writer.Write(stringIndexer(t.TeamName));
                    break;
                default:
                    throw new Exception($"Unexpected area member origin: {origin}");
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
        if (formatVersion > _currentFormatVersion ||
            formatVersion < _minSupportedFormatVersion)
            throw new InvalidDataException();

        // Read contents

        var issueIndex = new Dictionary<int, CrawledIssue>();

        using (var deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
        using (var reader = new BinaryReader(deflateStream, Encoding.UTF8))
        {
            // Read strings

            var stringCount = reader.ReadInt32();
            var stringIndex = new Dictionary<int, string?>()
            {
                { -1, null }
            };

            for (var i = 0; i < stringCount; i++)
            {
                var s = reader.ReadString();
                stringIndex.Add(i, s);
            }

            // Read area ownership

            var areaEntryCount = reader.ReadInt32();
            var areaEntries = new List<CrawledAreaEntry>(areaEntryCount);

            for (var i = 0; i < areaEntryCount; i++)
            {
                string label;
                string area;

                if (formatVersion == 11)
                {
                    area = stringIndex[reader.ReadInt32()]!;
                    label = "area-" + area;
                }
                else
                {
                    label = stringIndex[reader.ReadInt32()]!;
                    area = stringIndex[reader.ReadInt32()]!;
                }

                var leads = ReadAreaMembers(reader, stringIndex);
                var owners = ReadAreaMembers(reader, stringIndex);
                var entry = new CrawledAreaEntry(label, area, leads, owners);
                areaEntries.Add(entry);

                static CrawledAreaMember[] ReadAreaMembers(BinaryReader reader,
                                                    Dictionary<int, string?> stringIndex)
                {
                    var memberCount = reader.ReadInt32();
                    var members = new List<CrawledAreaMember>(memberCount);

                    for (var i = 0; i < memberCount; i++)
                    {
                        var userName = stringIndex[reader.ReadInt32()]!;
                        var origin = ReadAreaOrigin(reader, stringIndex);
                        var member = new CrawledAreaMember(origin, userName);
                        members.Add(member);
                    }

                    return members.ToArray();
                }

                static CrawledAreaMemberOrigin ReadAreaOrigin(BinaryReader reader,
                                                       Dictionary<int, string?> stringIndex)
                {
                    var kind = reader.ReadInt32();

                    switch (kind)
                    {
                        case 0: // Composite
                            {
                                var originCount = reader.ReadInt32();
                                var origins = new List<CrawledAreaMemberOrigin>(originCount);
                                for (var i = 0; i < originCount; i++)
                                {
                                    var origin = ReadAreaOrigin(reader, stringIndex);
                                    origins.Add(origin);
                                }
                                return new CrawledAreaMemberOrigin.Composite(origins);
                            }
                        case 1: // File
                            {
                                var orgName = stringIndex[reader.ReadInt32()]!;
                                var repoName = stringIndex[reader.ReadInt32()]!;
                                var path = stringIndex[reader.ReadInt32()]!;
                                var lineNumber = reader.ReadInt32();
                                return new CrawledAreaMemberOrigin.File(orgName, repoName, path, lineNumber);
                            }
                        case 2: // Teams
                            {
                                var orgName = stringIndex[reader.ReadInt32()]!;
                                var teamName = stringIndex[reader.ReadInt32()]!;
                                return new CrawledAreaMemberOrigin.Team(orgName, teamName);
                            }
                        default:
                            throw new Exception($"unexpected area origin kind: {kind}");
                    }
                }
            }

            var areaOwnership = new CrawledAreaOwnership(areaEntries.ToArray());

            // Read repos

            var repoCount = reader.ReadInt32();
            var repos = new List<CrawledRepo>(repoCount);

            for (var i = 0; i < repoCount; i++)
            {
                var repoId = reader.ReadInt64();
                var org = stringIndex[reader.ReadInt32()]!;
                var name = stringIndex[reader.ReadInt32()]!;
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
                        Name = stringIndex[reader.ReadInt32()]!,
                        Description = stringIndex[reader.ReadInt32()]!,
                        ColorText = stringIndex[reader.ReadInt32()]!
                    };
                    labelIndex.Add(labelId, label);
                    repo.Labels = repo.Labels.CopyAndAdd(label);
                }

                // Read milestones

                var milestoneCount = reader.ReadInt32();
                var milestoneIndex = new Dictionary<int, CrawledMilestone?>
                {
                    { -1, null }
                };

                for (var milestoneId = 0; milestoneId < milestoneCount; milestoneId++)
                {
                    var milestone = new CrawledMilestone
                    {
                        Id = reader.ReadInt64(),
                        Number = reader.ReadInt32(),
                        Title = stringIndex[reader.ReadInt32()]!,
                        Description = stringIndex[reader.ReadInt32()]!,
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
                        Title = stringIndex[reader.ReadInt32()]!,
                        // Body : ignored because we don't need here
                        CreatedAt = new DateTime(reader.ReadInt64()),
                        UpdatedAt = ToNullableDateTime(reader.ReadInt64()),
                        ClosedAt = ToNullableDateTime(reader.ReadInt64()),
                        CreatedBy = stringIndex[reader.ReadInt32()]!,
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
                        assignees.Add(stringIndex[reader.ReadInt32()]!);
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

            foreach (var repo in repos)
                repo.AreaOwnership = areaOwnership;

            return Task.FromResult(new CrawledIndex
            {
                Version = formatVersion,
                AreaOwnership = areaOwnership,
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
                                                      Dictionary<int, string?> stringIndex,
                                                      Dictionary<int, CrawledIssue> issueIndex)
        {
            var text = stringIndex[reader.ReadInt32()]!;

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
