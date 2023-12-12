using System.Collections.Frozen;

namespace IssueDb;

public sealed class CrawledAreaOwnership
{
    public static CrawledAreaOwnership Empty { get; } = new([]);

    private FrozenDictionary<string, CrawledAreaEntry>? _entryByLabel;

    public CrawledAreaOwnership(IReadOnlyList<CrawledAreaEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<CrawledAreaEntry> Entries { get; }

    public FrozenDictionary<string, CrawledAreaEntry> EntryByLabel
    {
        get
        {
            if (_entryByLabel is null)
            {
                var entryByLabel = Entries.ToFrozenDictionary(e => e.Label, StringComparer.OrdinalIgnoreCase);
                Interlocked.CompareExchange(ref _entryByLabel, entryByLabel, null);
            }

            return _entryByLabel;
        }
    }

    public CrawledAreaOwnership Merge(CrawledAreaOwnership other)
    {
        if (other is null || other == Empty)
            return this;

        if (this == Empty)
            return other;

        var entryByLabel = new Dictionary<string, CrawledAreaEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
            entryByLabel.Add(entry.Label, entry);

        foreach (var otherEntry in other.Entries)
        {
            if (entryByLabel.TryGetValue(otherEntry.Label, out var entry))
            {
                entryByLabel[entry.Label] = MergeEntries(entry, otherEntry);
            }
            else
            {
                entryByLabel.Add(otherEntry.Label, otherEntry);
            }
        }

        var mergedEntries = entryByLabel.Values.OrderBy(e => e.Label).ToArray();
        return new CrawledAreaOwnership(mergedEntries);

        static CrawledAreaEntry MergeEntries(CrawledAreaEntry entry, CrawledAreaEntry otherEntry)
        {
            var mergedLeads = MergeMembers(entry.Leads, otherEntry.Leads);
            var mergedOwners = MergeMembers(entry.Owners, otherEntry.Owners);
            return new CrawledAreaEntry(entry.Label, entry.Area, mergedLeads, mergedOwners);
        }

        static CrawledAreaMember[] MergeMembers(IReadOnlyList<CrawledAreaMember> members, IReadOnlyList<CrawledAreaMember> otherMembers)
        {
            var memberByName = new Dictionary<string, CrawledAreaMember>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in members)
                memberByName.Add(member.UserName, member);

            foreach (var otherMember in otherMembers)
            {
                if (memberByName.TryGetValue(otherMember.UserName, out var member))
                {
                    memberByName[member.UserName] = MergeMember(member, otherMember);
                }
                else
                {
                    memberByName.Add(otherMember.UserName, otherMember);
                }
            }
            return memberByName.Values.OrderBy(m => m.UserName).ToArray();
        }

        static CrawledAreaMember MergeMember(CrawledAreaMember member, CrawledAreaMember otherMember)
        {
            var origin = member.Origin.Merge(otherMember.Origin);
            return new CrawledAreaMember(origin, member.UserName);
        }
    }
}
