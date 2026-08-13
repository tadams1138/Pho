using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>
/// Application service for whole-tree edits made from the stub tree: deleting a multi-row
/// selection in one action, and rearranging rows by drag and drop. Both are expressed over the
/// same selection the UI holds, and both are recorded by the caller as a single undoable
/// revision. See F3/F7 in docs/spec/04-features.md.
/// </summary>
public sealed class TreeEditService
{
    private readonly GroupService _groups;
    private readonly StubService _stubs;

    public TreeEditService(GroupService groups, StubService stubs)
    {
        _groups = groups;
        _stubs = stubs;
    }

    /// <summary>What deleting the given selection would remove — for the confirmation prompt.</summary>
    public async Task<DeletionPlan> PlanDeletionAsync(IEnumerable<Guid> selectedIds)
        => TreeDeletion.Plan(await BuildTreeAsync(), selectedIds);

    /// <summary>
    /// Deletes every selected row. Selected groups are deleted whole (the cascade takes their
    /// descendants), so rows already inside a deleted group are not deleted twice.
    /// </summary>
    public async Task<DeletionPlan> DeleteAsync(IEnumerable<Guid> selectedIds)
    {
        var plan = TreeDeletion.Plan(await BuildTreeAsync(), selectedIds);

        foreach (var groupId in plan.GroupIds) await _groups.DeleteAsync(groupId);
        foreach (var stubId in plan.StubIds) await _stubs.DeleteAsync(stubId);

        return plan;
    }

    /// <summary>
    /// Moves every selected row into <paramref name="targetGroupId"/> (null = the tree root),
    /// skipping moves that are illegal or would change nothing. Returns how many rows moved.
    /// </summary>
    public async Task<int> MoveAsync(IEnumerable<Guid> selectedIds, Guid? targetGroupId)
    {
        var selected = selectedIds as ISet<Guid> ?? selectedIds.ToHashSet();
        if (selected.Count == 0) return 0;

        var groups = await _groups.ListAsync();
        var stubs = await _stubs.ListAsync();

        var movingGroups = TreeMoveRules.TopMostGroups(groups, selected)
            .Where(id => TreeMoveRules.CanMoveGroup(groups, id, targetGroupId))
            .ToList();

        // Groups that stay put still carry their stubs, so only the groups actually moving count.
        var movingStubs = TreeMoveRules
            .StubsToMove(groups, stubs, movingGroups, selected)
            .Select(id => stubs.First(s => s.Id == id))
            .Where(stub => TreeMoveRules.CanMoveStub(stub, targetGroupId))
            .ToList();

        foreach (var groupId in movingGroups) await _groups.MoveAsync(groupId, targetGroupId);
        foreach (var stub in movingStubs) await _stubs.MoveAsync(stub.Id, targetGroupId);

        return movingGroups.Count + movingStubs.Count;
    }

    private async Task<GroupNode> BuildTreeAsync()
        => GroupTree.Build(await _groups.ListAsync(), await _stubs.ListAsync());
}
