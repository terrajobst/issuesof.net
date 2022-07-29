using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using IssueDb.Querying.Binding;
using IssueDb.Querying.Ranges;

namespace IssueDb.Querying
{
    public sealed partial class IssueQuery
    {
        private static readonly Dictionary<(string Key, string Value), Func<IssueFilter, BoundKeyValueQuery, bool>> _keyValueHandlers = CreateKeyValueHandlers();

        private static Dictionary<(string Key, string Value), Func<IssueFilter, BoundKeyValueQuery, bool>> CreateKeyValueHandlers()
        {
            var result = new Dictionary<(string Key, string Value), Func<IssueFilter, BoundKeyValueQuery, bool>>();
            var methods = typeof(IssueQuery).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttributesData()
                                      .SingleOrDefault(ca => ca.AttributeType == typeof(KeyValueHandlerAttribute));
                if (attribute is null)
                    continue;

                if (attribute.ConstructorArguments.Count != 1)
                    throw new Exception($"Wrong number of arguments for [{nameof(KeyValueHandlerAttribute)}] on {method}");

                if (attribute.ConstructorArguments[0].ArgumentType != typeof(string[]))
                    throw new Exception($"Wrong type of arguments for [{nameof(KeyValueHandlerAttribute)}] on {method}");

                var args = (ICollection<CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value;

                if (args.Count == 0)
                    throw new Exception($"Wrong number of arguments for [{nameof(KeyValueHandlerAttribute)}] on {method}");

                var strings = args.Select(a => (string)a.Value);
                var pairs = GetKeyValues(strings).ToArray();
                var parameters = method.GetParameters();

                Func<IssueFilter, BoundKeyValueQuery, bool> handler;

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IssueFilter))
                {
                    handler = (filter, query) =>
                    {
                        method.Invoke(null, new object[] { filter });
                        return true;
                    };
                }
                else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(IssueFilter) &&
                                                   parameters[1].ParameterType == typeof(BoundKeyValueQuery))
                {
                    handler = (filter, query) =>
                    {
                        method.Invoke(null, new object[] { filter, query });
                        return true;
                    };
                }
                else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(IssueFilter) &&
                                                   parameters[1].ParameterType == typeof(BoundKeyValueQuery) &&
                                                   parameters[2].ParameterType == typeof(RangeSyntax<DateTimeOffset>))
                {
                    handler = (filter, query) =>
                    {
                        if (RangeSyntax.ParseDateTimeOffset(query.Value) is RangeSyntax<DateTimeOffset> r)
                        {
                            method.Invoke(null, new object[] { filter, query, r });
                            return true;
                        }

                        return false;
                    };
                }
                else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(IssueFilter) &&
                                                   parameters[1].ParameterType == typeof(BoundKeyValueQuery) &&
                                                   parameters[2].ParameterType == typeof(RangeSyntax<int>))
                {
                    handler = (filter, query) =>
                    {
                        if (RangeSyntax.ParseInt32(query.Value) is RangeSyntax<int> r)
                        {
                            method.Invoke(null, new object[] { filter, query, r });
                            return true;
                        }

                        return false;
                    };
                }
                else
                {
                    throw new Exception($"Unexpected signature for {method}");
                }

                foreach (var kv in pairs)
                    result.Add(kv, handler);
            }

            return result;
        }

        private static IEnumerable<(string Key, string Value)> GetKeyValues(IEnumerable<string> pairs)
        {
            foreach (var pair in pairs)
            {
                var kv = pair.Split(":");
                if (kv.Length == 1)
                    yield return (kv[0], null);
                else if (kv.Length == 2)
                    yield return (kv[0], kv[1]);
                else
                    throw new ArgumentException($"Invalid syntax: '{pair}'", nameof(pairs));
            }
        }

#pragma warning disable IDE0051 // Remove unused private members

        [KeyValueHandler("is:open", "state:open")]
        private static void ApplyIsOpen(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsOpen = !query.IsNegated;
        }

        [KeyValueHandler("is:closed", "state:closed")]
        private static void ApplyIsClosed(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsOpen = query.IsNegated;
        }

        [KeyValueHandler("is:locked")]
        private static void ApplyIsLocked(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsLocked = !query.IsNegated;
        }

        [KeyValueHandler("is:unlocked")]
        private static void ApplyIsUnlocked(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsLocked = query.IsNegated;
        }

        [KeyValueHandler("is:pr", "type:pr")]
        private static void ApplyIsPr(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsPullRequest = !query.IsNegated;
        }

        [KeyValueHandler("is:issue", "type:issue")]
        private static void ApplyIsIssue(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsPullRequest = query.IsNegated;
        }

        [KeyValueHandler("is:merged", "state:merged")]
        private static void ApplyIsMerged(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsMerged = !query.IsNegated;
        }

        [KeyValueHandler("is:unmerged", "state:unmerged")]
        private static void ApplyIsUnmerged(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsMerged = query.IsNegated;
        }

        [KeyValueHandler("is:draft", "draft:true")]
        private static void ApplyIsDraft(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsDraft = !query.IsNegated;
        }

        [KeyValueHandler("draft:false")]
        private static void ApplyDraftFalse(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsDraft = query.IsNegated;
        }

        [KeyValueHandler("archived:true")]
        private static void ApplyArchivedTrue(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsArchived = !query.IsNegated;
        }

        [KeyValueHandler("archived:false")]
        private static void ApplyArchivedFalse(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.IsArchived = query.IsNegated;
        }

        [KeyValueHandler("no:assignee")]
        private static void ApplyNoAssignee(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoAssignees = !query.IsNegated;
        }

        [KeyValueHandler("no:label")]
        private static void ApplyNoLabel(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoLabels = !query.IsNegated;
        }

        [KeyValueHandler("no:area")]
        private static void ApplyNoArea(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoArea = !query.IsNegated;
        }

        [KeyValueHandler("no:area-lead")]
        private static void ApplyNoAreaLead(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoAreaLead = !query.IsNegated;
        }

        [KeyValueHandler("no:area-pod")]
        private static void ApplyNoAreaPod(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoAreaPod = !query.IsNegated;
        }

        [KeyValueHandler("no:area-owner")]
        private static void ApplyNoAreaOwner(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoAreaOwner = !query.IsNegated;
        }

        [KeyValueHandler("no:milestone")]
        private static void ApplyNoMilestone(IssueFilter filter, BoundKeyValueQuery query)
        {
            filter.NoMilestone = !query.IsNegated;
        }

        [KeyValueHandler("org")]
        private static void ApplyOrg(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedOrgs.Add(query.Value);
            else
                filter.IncludedOrgs.Add(query.Value);
        }

        [KeyValueHandler("repo")]
        private static void ApplyRepo(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedRepos.Add(query.Value);
            else
                filter.IncludedRepos.Add(query.Value);
        }

        [KeyValueHandler("author")]
        private static void ApplyAuthor(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAuthors.Add(query.Value);
            else
                filter.Author = query.Value;
        }

        [KeyValueHandler("assignee")]
        private static void ApplyAssignee(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAssignees.Add(query.Value);
            else
                filter.IncludedAssignees.Add(query.Value);
        }

        [KeyValueHandler("label")]
        private static void ApplyLabel(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedLabels.Add(query.Value);
            else
                filter.IncludedLabels.Add(query.Value);
        }

        [KeyValueHandler("milestone")]
        private static void ApplyMilestone(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedMilestones.Add(query.Value);
            else
                filter.Milestone = query.Value;
        }

        [KeyValueHandler("area")]
        private static void ApplyArea(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedLabels.Add($"area-{query.Value}");
            else
                filter.IncludedLabels.Add($"area-{query.Value}");
        }

        [KeyValueHandler("area-under")]
        private static void ApplyAreaUnder(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAreas.Add(query.Value);
            else
                filter.IncludedAreas.Add(query.Value);
        }

        [KeyValueHandler("area-node")]
        private static void ApplyAreaNode(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAreaNodes.Add(query.Value);
            else
                filter.IncludedAreaNodes.Add(query.Value);
        }

        [KeyValueHandler("area-lead")]
        private static void ApplyAreaLead(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAreaLeads.Add(query.Value);
            else
                filter.IncludedAreaLeads.Add(query.Value);
        }

        [KeyValueHandler("area-pod")]
        private static void ApplyAreaPod(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAreaPods.Add(query.Value);
            else
                filter.IncludedAreaPods.Add(query.Value);
        }

        [KeyValueHandler("area-owner")]
        private static void ApplyAreaOwner(IssueFilter filter, BoundKeyValueQuery query)
        {
            if (query.IsNegated)
                filter.ExcludedAreaOwners.Add(query.Value);
            else
                filter.IncludedAreaOwners.Add(query.Value);
        }

        [KeyValueHandler("created")]
        private static void ApplyCreated(IssueFilter filter, BoundKeyValueQuery query, RangeSyntax<DateTimeOffset> range)
        {
            filter.Created = range.Negate(query.IsNegated);
        }

        [KeyValueHandler("updated")]
        private static void ApplyUpdated(IssueFilter filter, BoundKeyValueQuery query, RangeSyntax<DateTimeOffset> range)
        {
            filter.Updated = range.Negate(query.IsNegated);
        }

        [KeyValueHandler("closed")]
        private static void ApplyClosed(IssueFilter filter, BoundKeyValueQuery query, RangeSyntax<DateTimeOffset> range)
        {
            filter.Closed = range.Negate(query.IsNegated);
        }

        [KeyValueHandler("comments")]
        private static void ApplyComments(IssueFilter filter, BoundKeyValueQuery query, RangeSyntax<int> range)
        {
            filter.Comments = range.Negate(query.IsNegated);
        }

        [KeyValueHandler("reactions")]
        private static void ApplyReactions(IssueFilter filter, BoundKeyValueQuery query, RangeSyntax<int> range)
        {
            filter.Reactions = range.Negate(query.IsNegated);
        }

        [KeyValueHandler("interactions")]
        private static void ApplyInteractions(IssueFilter filter, BoundKeyValueQuery query, RangeSyntax<int> range)
        {
            filter.Interactions = range.Negate(query.IsNegated);
        }

        [KeyValueHandler("sort:created", "sort:created-asc")]
        private static void ApplySortCreated(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.CreatedAscending);
        }

        [KeyValueHandler("sort:created-desc")]
        private static void ApplySortCreatedDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.CreatedDescending);
        }

        [KeyValueHandler("sort:updated", "sort:updated-asc")]
        private static void ApplySortUpdated(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.UpdatedAscending);
        }

        [KeyValueHandler("sort:updated-desc")]
        private static void ApplySortUpdatedDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.UpdatedDescending);
        }

        [KeyValueHandler("sort:comments", "sort:comments-asc")]
        private static void ApplySortComments(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.CommentsAscending);
        }

        [KeyValueHandler("sort:comments-desc")]
        private static void ApplySortCommentsDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.CommentsDescending);
        }

        [KeyValueHandler("sort:reactions", "sort:reactions-asc")]
        private static void ApplySortReactions(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsAscending);
        }

        [KeyValueHandler("sort:reactions-desc")]
        private static void ApplySortReactionsDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsDescending);
        }

        [KeyValueHandler("sort:reactions-+1", "sort:reactions-+1-asc")]
        private static void ApplySortReactionsPlus1(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsPlus1Ascending);
        }

        [KeyValueHandler("sort:reactions-+1-desc")]
        private static void ApplySortReactionsPlus1Desc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsPlus1Descending);
        }

        [KeyValueHandler("sort:reactions--1", "sort:reactions--1-asc")]
        private static void ApplySortReactionsMinus1(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsMinus1Ascending);
        }

        [KeyValueHandler("sort:reactions--1-desc")]
        private static void ApplySortReactionsMinus1Desc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsMinus1Descending);
        }

        [KeyValueHandler("sort:reactions-smile", "sort:reactions-smile-asc")]
        private static void ApplySortReactionsSmile(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsSmileAscending);
        }

        [KeyValueHandler("sort:reactions-smile-desc")]
        private static void ApplySortReactionsSmileDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsSmileDescending);
        }

        [KeyValueHandler("sort:reactions-heart", "sort:reactions-heart-asc")]
        private static void ApplySortReactionsHeart(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsHeartAscending);
        }

        [KeyValueHandler("sort:reactions-heart-desc")]
        private static void ApplySortReactionsHeartDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsHeartDescending);
        }

        [KeyValueHandler("sort:reactions-tada", "sort:reactions-tada-asc")]
        private static void ApplySortReactionsTada(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsTadaAscending);
        }

        [KeyValueHandler("sort:reactions-tada-desc")]
        private static void ApplySortReactionsTadaDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsTadaDescending);
        }

        [KeyValueHandler("sort:reactions-thinking_face", "sort:reactions-thinking_face-asc")]
        private static void ApplySortReactionsThinkingFace(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsThinkingFaceAscending);
        }

        [KeyValueHandler("sort:reactions-thinking_face-desc")]
        private static void ApplySortReactionsThinkingFaceDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.ReactionsThinkingFaceDescending);
        }

        [KeyValueHandler("sort:interactions", "sort:interactions-asc")]
        private static void ApplySortInteractions(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.InteractionsAscending);
        }

        [KeyValueHandler("sort:interactions-desc")]
        private static void ApplySortInteractionsDesc(IssueFilter filter)
        {
            filter.Sort.Add(IssueSort.InteractionsDescending);
        }

        [KeyValueHandler("group:org")]
        private static void ApplyGroupOrg(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Org);
        }

        [KeyValueHandler("group:repo")]
        private static void ApplyGroupRepo(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Repo);
        }

        [KeyValueHandler("group:author")]
        private static void ApplyGroupAuthor(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Author);
        }

        [KeyValueHandler("group:assignee")]
        private static void ApplyGroupAssignee(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Assignee);
        }

        [KeyValueHandler("group:label")]
        private static void ApplyGroupLabel(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Label);
        }

        [KeyValueHandler("group:milestone")]
        private static void ApplyGroupMilestone(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Milestone);
        }

        [KeyValueHandler("group:area")]
        private static void ApplyGroupArea(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.Area);
        }

        [KeyValueHandler("group:area-node")]
        private static void ApplyGroupAreaNode(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.AreaNode);
        }

        [KeyValueHandler("group:area-under")]
        private static void ApplyGroupAreaUnder(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.AreaUnder);
        }

        [KeyValueHandler("group:area-lead")]
        private static void ApplyGroupAreaLead(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.AreaLead);
        }

        [KeyValueHandler("group:area-pod")]
        private static void ApplyGroupAreaPod(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.AreaPod);
        }

        [KeyValueHandler("group:area-owner")]
        private static void ApplyGroupAreaOwner(IssueFilter filter)
        {
            filter.Groups.Add(IssueGroup.AreaOwner);
        }

        [KeyValueHandler("group-sort:key", "group-sort:key-asc")]
        private static void ApplyGroupSortKey(IssueFilter filter)
        {
            filter.GroupSort.Add(IssueGroupSort.KeyAscending);
        }

        [KeyValueHandler("group-sort:key-desc")]
        private static void ApplyGroupSortKeyDesc(IssueFilter filter)
        {
            filter.GroupSort.Add(IssueGroupSort.KeyDescending);
        }

        [KeyValueHandler("group-sort:count", "group-sort:count-asc")]
        private static void ApplyGroupSortCount(IssueFilter filter)
        {
            filter.GroupSort.Add(IssueGroupSort.CountAscending);
        }

        [KeyValueHandler("group-sort:count-desc")]
        private static void ApplyGroupSortCountDesc(IssueFilter filter)
        {
            filter.GroupSort.Add(IssueGroupSort.CountDescending);
        }

#pragma warning restore IDE0051 // Remove unused private members

        [AttributeUsage(AttributeTargets.Method)]
        private sealed class KeyValueHandlerAttribute : Attribute
        {
#pragma warning disable IDE0060 // Remove unused parameter
            public KeyValueHandlerAttribute(params string[] pairs)
#pragma warning restore IDE0060 // Remove unused parameter
            {
            }
        }
    }
}
