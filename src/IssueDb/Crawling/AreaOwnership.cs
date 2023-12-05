using System.Collections.Frozen;

namespace IssueDb;

public sealed class AreaOwnership
{
    public static AreaOwnership Empty { get; } = new([]);

    private FrozenDictionary<string, AreaEntry> _entryByName;

    public AreaOwnership(IReadOnlyList<AreaEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<AreaEntry> Entries { get; }

    public FrozenDictionary<string, AreaEntry> EntryByName
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

    public AreaOwnership Merge(AreaOwnership other)
    {
        if (other is null || other == Empty)
            return this;

        if (this == Empty)
            return other;

        var entryByName = new Dictionary<string, AreaEntry>(StringComparer.OrdinalIgnoreCase);

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
        return new AreaOwnership(mergedEntries);

        static AreaEntry MergeEntries(AreaEntry entry, AreaEntry otherEntry)
        {
            var mergedLeads = MergeMembers(entry.Leads, otherEntry.Leads);
            var mergedOwners = MergeMembers(entry.Owners, otherEntry.Owners);
            return new AreaEntry(entry.Area, mergedLeads, mergedOwners);
        }

        static AreaMember[] MergeMembers(IReadOnlyList<AreaMember> members, IReadOnlyList<AreaMember> otherMembers)
        {
            var memberByName = new Dictionary<string, AreaMember>(StringComparer.OrdinalIgnoreCase);

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

        static AreaMember MergeMember(AreaMember member, AreaMember otherMember)
        {
            var origin = member.Origin.Merge(otherMember.Origin);
            return new AreaMember(origin, member.UserName);
        }
    }
}
