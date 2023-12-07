using IssueDb.Querying;

namespace IssueDb.Crawling;

public abstract partial class CrawledIssueResults
{
    protected const int ItemsPerPage = 100;

    public int PageCount => (int)Math.Ceiling(ItemCount / (float)ItemsPerPage);

    public abstract IReadOnlyCollection<IssueSort> Sorts { get; }

    public virtual IReadOnlyCollection<CrawledIssueGroupKey> GroupKeys => Array.Empty<CrawledIssueGroupKey>();

    public abstract int ItemCount { get; }

    public abstract int IssueCount { get; }

    public bool IsGrouped => GroupKeys.Count > 0;

    public abstract IEnumerable<CrawledIssueOrGroup> Roots { get; }

    public virtual bool IsExpanded(CrawledIssueGroup group) => false;

    public virtual void ExpandAll()
    {
    }

    public virtual void CollapseAll()
    {
    }

    public virtual void Expand(CrawledIssueGroup group)
    {
    }

    public virtual void Collapse(CrawledIssueGroup group)
    {
    }

    public abstract IEnumerable<CrawledIssueOrGroup> GetPage(int pageNumber);

    public static CrawledIssueResults Empty => new ArrayIssueResults(Array.Empty<CrawledIssue>(), Array.Empty<IssueSort>());

    public static CrawledIssueResults Create(IEnumerable<CrawledIssue> issues, IReadOnlyCollection<IssueSort> sorts)
    {
        return new ArrayIssueResults(issues.Sort(sorts).ToArray(), sorts);
    }

    public static CrawledIssueResults Create(IEnumerable<CrawledIssue> issues, IReadOnlyCollection<IssueSort> sorts, CrawledIssueGroupKey[] keys, IReadOnlyCollection<IssueGroupSort> groupSorts)
    {
        if (keys is null || keys.Length == 0)
            throw new ArgumentException("Must pass in non-empty keys", nameof(keys));

        var topLevelGroups = Group(issues.Sort(sorts), keys)
                                .Select(r => r.ToGroup())
                                .ToArray();

        SortGroups(topLevelGroups, groupSorts);

        return new GroupedIssueResults(keys, topLevelGroups, sorts);

        static CrawledIssueOrGroup[] Group(IEnumerable<CrawledIssue> issues, CrawledIssueGroupKey[] fields)
        {
            var field = fields[0];
            var topLevel = GroupFirst(null, issues, field);

            GroupNext(topLevel, fields, 1);

            return topLevel;
        }

        static CrawledIssueOrGroup[] GroupFirst(CrawledIssueGroup? parent, IEnumerable<CrawledIssue> issues, CrawledIssueGroupKey key)
        {
            return key.Apply(issues)
                      .Select(g => (CrawledIssueOrGroup)new CrawledIssueGroup(CombineKeys(parent, g.Key), g.Select(i => (CrawledIssueOrGroup)i).ToArray()))
                      .ToArray();
        }

        static void GroupNext(CrawledIssueOrGroup[] current, CrawledIssueGroupKey[] fields, int fieldIndex)
        {
            if (fieldIndex >= fields.Length)
                return;

            var field = fields[fieldIndex];

            for (var i = 0; i < current.Length; i++)
            {
                var oldGroup = current[i].ToGroup();
                var oldChildren = oldGroup.Children.Select(c => c.ToIssue());
                var newChildren = GroupFirst(oldGroup, oldChildren, field);
                var newGroup = new CrawledIssueGroup(oldGroup.Keys, newChildren);
                current[i] = newGroup;

                GroupNext(newGroup.Children, fields, fieldIndex + 1);
            }
        }

        static string[] CombineKeys(CrawledIssueGroup? parent, string key)
        {
            if (parent is null)
                return new[] { key };

            var result = new string[parent.Keys.Length + 1];
            Array.Copy(parent.Keys, result, parent.Keys.Length);
            result[parent.Keys.Length] = key;
            return result;
        }

        static void SortGroup(CrawledIssueGroup group, IEnumerable<IssueGroupSort> sorts)
        {
            var childrenAsGroups = group.Children.Where(c => c.IsGroup)
                                                 .Select(c => c.ToGroup())
                                                 .Sort(sorts)
                                                 .ToArray();

            for (var i = 0; i < childrenAsGroups.Length; i++)
            {
                group.Children[i] = childrenAsGroups[i];
                SortGroup(childrenAsGroups[i], sorts);
            }
        }

        static void SortGroups(CrawledIssueGroup[] groups, IEnumerable<IssueGroupSort> sorts)
        {
            var sortedGroups = groups.Sort(sorts).ToArray();
            for (var i = 0; i < sortedGroups.Length; i++)
            {
                groups[i] = sortedGroups[i];
                SortGroup(groups[i], sorts);
            }
        }
    }
}
