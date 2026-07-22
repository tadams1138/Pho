using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>
/// Application service for the group tree (F3): create, rename, move (with cycle prevention),
/// and cascade delete. Deleting a group removes all descendant groups and every stub within
/// them. See docs/spec/03-domain-model.md and F3 in docs/spec/04-features.md.
/// </summary>
public sealed class GroupService
{
    private readonly IGroupRepository _groups;
    private readonly IStubRepository _stubs;

    public GroupService(IGroupRepository groups, IStubRepository stubs)
    {
        _groups = groups;
        _stubs = stubs;
    }

    public Task<IReadOnlyList<Group>> ListAsync() => _groups.ListAsync();

    public async Task<Group> CreateAsync(string name, Guid? parentId = null)
    {
        var group = new Group { Name = name, ParentGroupId = parentId };
        await _groups.AddAsync(group);
        return group;
    }

    public async Task RenameAsync(Guid id, string name)
    {
        var group = await _groups.GetAsync(id)
            ?? throw new InvalidOperationException($"Group {id} not found.");
        group.Name = name;
        await _groups.UpdateAsync(group);
    }

    public async Task MoveAsync(Guid id, Guid? newParentId)
    {
        if (newParentId == id)
            throw new InvalidOperationException("A group cannot be its own parent.");

        var all = await _groups.ListAsync();
        if (newParentId is Guid parent && DescendantIds(all, id).Contains(parent))
            throw new InvalidOperationException("Cannot move a group into its own descendant.");

        var group = await _groups.GetAsync(id)
            ?? throw new InvalidOperationException($"Group {id} not found.");
        group.ParentGroupId = newParentId;
        await _groups.UpdateAsync(group);
    }

    public async Task DeleteAsync(Guid id)
    {
        var all = await _groups.ListAsync();
        var subtree = DescendantIds(all, id);

        var stubs = await _stubs.ListAsync();
        foreach (var stub in stubs.Where(s => s.GroupId is Guid gid && subtree.Contains(gid)).ToList())
            await _stubs.DeleteAsync(stub.Id);

        foreach (var groupId in subtree)
            await _groups.DeleteAsync(groupId);
    }

    // The id plus every group transitively parented under it.
    private static HashSet<Guid> DescendantIds(IReadOnlyList<Group> all, Guid root)
    {
        var set = new HashSet<Guid> { root };
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var group in all)
            {
                if (group.ParentGroupId is Guid parent && set.Contains(parent) && set.Add(group.Id))
                    changed = true;
            }
        }

        return set;
    }
}
