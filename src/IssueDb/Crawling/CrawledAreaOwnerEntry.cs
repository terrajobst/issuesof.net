namespace IssueDb;

#warning Remove me
public sealed class CrawledAreaOwnerEntry
{
    public CrawledAreaOwnerEntry(string area, IReadOnlyList<string> leads, IReadOnlyList<string> owners)
    {
        Area = area;
        Leads = leads;
        Owners = owners;
    }

    public string Area { get; }
    public IReadOnlyList<string> Leads { get; }
    public IReadOnlyList<string> Owners { get; }
}

#warning Split these into files and rename them with the Crawled prefix

public sealed class AreaOwnership
{
    public static AreaOwnership Empty { get; } = new([]);

    public AreaOwnership(IReadOnlyList<AreaEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<AreaEntry> Entries { get; }

    public AreaOwnership Merge(AreaOwnership other)
    {
        if (this == Empty)
            return other;

        if (other == Empty)
            return this;

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
            var result = new List<AreaMember>();
            var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in members.Concat(otherMembers))
            {
                if (seenUserNames.Add(member.UserName))
                    result.Add(member);
            }

            return result.ToArray();
        }
    }
}

// TODO: Consider splitting owners into owners and subscribers
//       We could decide that ownership defined via teams only
//       counts as subscribers when merging

public sealed class AreaEntry
{
    public AreaEntry(string area,
                     IReadOnlyList<AreaMember> leads,
                     IReadOnlyList<AreaMember> owners)
    {
        Area = area;
        Leads = leads;
        Owners = owners;
    }

    public string Area { get; }
    public IReadOnlyList<AreaMember> Leads { get; }
    public IReadOnlyList<AreaMember> Owners { get; }
}

public sealed class AreaMember
{
    public AreaMember(AreaMemberOrigin origin,
                      string userName)
    {
        Origin = origin;
        UserName = userName;
    }

    public AreaMemberOrigin Origin { get; }
    public string UserName { get; }
}

public abstract class AreaMemberOrigin
{
    public static AreaMemberOrigin FromComposite(params AreaMemberOrigin[] origins)
    {
        return new Composite(origins);
    }

    public static AreaMemberOrigin FromFile(string orgName, string repoName, string path, int lineNumber)
    {
        return new File(orgName, repoName, path, lineNumber);
    }

    public static AreaMemberOrigin FromTeam(string orgName, string teamName)
    {
        return new Team(orgName, teamName);
    }

    public sealed class Composite(IReadOnlyList<AreaMemberOrigin> origins) : AreaMemberOrigin
    {
        public IReadOnlyList<AreaMemberOrigin> Origins { get; } = origins;
    }

    public sealed class File(string orgName, string repoName, string path, int lineNumber) : AreaMemberOrigin
    {
        public string OrgName { get; } = orgName;
        public string RepoName { get; } = repoName;
        public string Path { get; } = path;
        public int LineNumber { get; } = lineNumber;
    }

    public sealed class Team(string orgName, string teamName) : AreaMemberOrigin
    {
        public string OrgName { get; } = orgName;
        public string TeamName { get; } = teamName;
    }
}
