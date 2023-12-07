using System.Collections.Frozen;

namespace IssueDb;

public sealed class CrawledAreaOwnership
{
    public static CrawledAreaOwnership Empty { get; } = new([]);

    private FrozenDictionary<string, CrawledAreaEntry>? _entryByName;

    public CrawledAreaOwnership(IReadOnlyList<CrawledAreaEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<CrawledAreaEntry> Entries { get; }

    public FrozenDictionary<string, CrawledAreaEntry> EntryByName
    {
        get
        {
            if (_entryByName is null)
            {
                var entryByName = Entries.ToFrozenDictionary(e => e.Area);
                Interlocked.CompareExchange(ref _entryByName, entryByName, null);
            }

            return _entryByName;
        }
    }

    public CrawledAreaOwnership Merge(CrawledAreaOwnership other)
    {
        if (other is null || other == Empty)
            return this;

        if (this == Empty)
            return other;

        var entryByName = new Dictionary<string, CrawledAreaEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in Entries)
            entryByName.Add(entry.Area, entry);

        foreach (var otherEntry in other.Entries)
        {
            if (entryByName.TryGetValue(otherEntry.Area, out var entry))
            {
                entryByName[entry.Area] = MergeEntries(entry, otherEntry);
            }
            else
            {
                entryByName.Add(otherEntry.Area, otherEntry);
            }
        }

        var mergedEntries = entryByName.Values.OrderBy(e => e.Area).ToArray();
        return new CrawledAreaOwnership(mergedEntries);

        static CrawledAreaEntry MergeEntries(CrawledAreaEntry entry, CrawledAreaEntry otherEntry)
        {
            var mergedLeads = MergeMembers(entry.Leads, otherEntry.Leads);
            var mergedOwners = MergeMembers(entry.Owners, otherEntry.Owners);
            return new CrawledAreaEntry(entry.Area, mergedLeads, mergedOwners);
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
