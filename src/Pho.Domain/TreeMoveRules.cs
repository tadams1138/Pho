using System;
using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

/// <summary>
/// Pure rules for rearranging the tree by drag and drop (F3): which drops are legal, and how a
/// multi-row selection is reduced to the moves that actually need to happen.
/// </summary>
public static class TreeMoveRules
{
    /// <summary>
    /// A group may move under any group that is not itself or one of its own descendants (that
    /// would orphan the subtree), and dropping it back where it already is does nothing.
    /// </summary>
    public static bool CanMoveGroup(IReadOnlyList<Group> groups, Guid groupId, Guid? newParentId)
    {
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return false;
        if (group.ParentGroupId == newParentId) return false;
        if (newParentId is not Guid parent) return true;

        return !Subtree(groups, groupId).Contains(parent);
    }

    /// <summary>A stub may move to any group (or to the root) other than the one holding it.</summary>
    public static bool CanMoveStub(Stub stub, Guid? newGroupId) => stub.GroupId != newGroupId;

    /// <summary>
    /// Reduces selected groups to those with no selected ancestor — moving a parent already carries
    /// its children, and moving both would fight over the destination.
    /// </summary>
    public static IReadOnlyList<Guid> TopMostGroups(IReadOnlyList<Group> groups, IEnumerable<Guid> selectedIds)
    {
        var selected = selectedIds as ISet<Guid> ?? selectedIds.ToHashSet();
        var byId = groups.ToDictionary(g => g.Id);

        return selected
            .Where(byId.ContainsKey)
            .Where(id => !HasSelectedAncestor(byId, selected, id))
            .ToList();
    }

    /// <summary>
    /// The selected stubs that need moving themselves — a stub nested inside a group that is
    /// moving keeps its group and travels with it.
    /// </summary>
    public static IReadOnlyList<Guid> StubsToMove(
        IReadOnlyList<Group> groups,
        IReadOnlyList<Stub> stubs,
        IEnumerable<Guid> movingGroupIds,
        IEnumerable<Guid> selectedIds)
    {
        var selected = selectedIds as ISet<Guid> ?? selectedIds.ToHashSet();
        var moving = new HashSet<Guid>();
        foreach (var groupId in movingGroupIds)
        {
            moving.UnionWith(Subtree(groups, groupId));
        }

        return stubs
            .Where(s => selected.Contains(s.Id))
            .Where(s => s.GroupId is not Guid gid || !moving.Contains(gid))
            .Select(s => s.Id)
            .ToList();
    }

    /// <summary>The group plus every group transitively nested under it.</summary>
    public static HashSet<Guid> Subtree(IReadOnlyList<Group> groups, Guid root)
    {
        var set = new HashSet<Guid> { root };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var group in groups)
            {
                if (group.ParentGroupId is Guid parent && set.Contains(parent) && set.Add(group.Id))
                    changed = true;
            }
        }

        return set;
    }

    private static bool HasSelectedAncestor(IReadOnlyDictionary<Guid, Group> byId, ISet<Guid> selected, Guid id)
    {
        var current = byId[id].ParentGroupId;
        while (current is Guid parent)
        {
            if (selected.Contains(parent)) return true;
            current = byId.TryGetValue(parent, out var group) ? group.ParentGroupId : null;
        }

        return false;
    }
}
