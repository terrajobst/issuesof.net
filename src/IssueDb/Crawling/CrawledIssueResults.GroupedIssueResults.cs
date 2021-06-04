using System.Collections.Generic;
using System.Linq;

using IssueDb.Querying;

namespace IssueDb.Crawling
{
    public abstract partial class CrawledIssueResults
    {
        private sealed class GroupedIssueResults : CrawledIssueResults
        {
            private readonly IReadOnlyCollection<IssueSort> _sorts;
            private readonly CrawledIssueGroupKey[] _keys;
            private readonly CrawledIssueGroup[] _groups;
            private readonly HashSet<CrawledIssueGroup> _expandedGroups = new HashSet<CrawledIssueGroup>();
            private int _itemCount;
            private int _issueCount;

            public GroupedIssueResults(CrawledIssueGroupKey[] keys, CrawledIssueGroup[] groups, IReadOnlyCollection<IssueSort> sorts)
            {
                _keys = keys;
                _groups = groups;
                _sorts = sorts;
                UpdateCounts();
            }

            public override IReadOnlyCollection<IssueSort> Sorts => _sorts;

            public override IReadOnlyCollection<CrawledIssueGroupKey> GroupKeys => _keys;

            public override int ItemCount => _itemCount;

            public override int IssueCount => _issueCount;

            public override IEnumerable<CrawledIssueOrGroup> Roots => _groups.Select(g => (CrawledIssueOrGroup)g);

            public override bool IsExpanded(CrawledIssueGroup group) => _expandedGroups.Contains(group);

            public override void ExpandAll()
            {
                foreach (var group in _groups)
                    Walk(group);

                UpdateCounts();

                void Walk(CrawledIssueGroup group)
                {
                    _expandedGroups.Add(group);

                    foreach (var child in group.Children)
                    {
                        if (child.IsGroup)
                            Walk(child.ToGroup());
                    }
                }
            }

            public override void CollapseAll()
            {
                _expandedGroups.Clear();
                UpdateCounts();
            }

            public override void Expand(CrawledIssueGroup group)
            {
                _expandedGroups.Add(group);
                UpdateCounts();
            }

            public override void Collapse(CrawledIssueGroup group)
            {
                _expandedGroups.Remove(group);
                UpdateCounts();
            }

            private void UpdateCounts()
            {
                _itemCount = 0;
                _issueCount = 0;

                foreach (var group in _groups)
                {
                    Walk(group);
                    WalkVisible(group);
                }

                void Walk(CrawledIssueGroup group)
                {
                    foreach (var child in group.Children)
                    {
                        if (child.IsGroup)
                            Walk(child.ToGroup());
                        else
                            _issueCount++;
                    }
                }

                void WalkVisible(CrawledIssueGroup group)
                {
                    _itemCount++;

                    if (!IsExpanded(group))
                        return;

                    foreach (var child in group.Children)
                    {
                        if (child.IsGroup)
                            WalkVisible(child.ToGroup());
                        else
                            _itemCount++;
                    }
                }
            }

            public override IEnumerable<CrawledIssueOrGroup> GetPage(int pageNumber)
            {
                var result = new List<CrawledIssueOrGroup>(PageCount);
                var itemsToSkip = (pageNumber - 1) * ItemsPerPage;

                foreach (var group in _groups)
                    Walk(result, _expandedGroups, null, group, ref itemsToSkip);

                return result;

                static void Walk(List<CrawledIssueOrGroup> result, HashSet<CrawledIssueGroup> expandedGroups, CrawledIssueGroup parent, CrawledIssueOrGroup item, ref int itemsToSkip)
                {
                    if (result.Count == ItemsPerPage)
                        return;

                    if (itemsToSkip > 0)
                    {
                        itemsToSkip--;
                    }
                    else
                    {
                        // If this is the first item, ensure we add the group header
                        if (result.Count == 0 && item.IsIssue && parent != null)
                            result.Add(parent);

                        result.Add(item);
                    }

                    if (item.IsGroup)
                    {
                        var group = item.ToGroup();

                        if (expandedGroups.Contains(group))
                        {
                            foreach (var child in group.Children)
                                Walk(result, expandedGroups, group, child, ref itemsToSkip);
                        }
                    }
                }
            }
        }
    }
}
