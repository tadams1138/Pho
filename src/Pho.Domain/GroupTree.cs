using System;
using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

/// <summary>One node in the stub tree: a group (null at the root), its child groups, and its stubs.</summary>
public sealed record GroupNode(Group? Group, IReadOnlyList<GroupNode> Children, IReadOnlyList<Stub> Stubs);

/// <summary>
/// Builds the nested tree of groups and stubs for the UI. The root node has a null Group and holds
/// the top-level groups plus ungrouped stubs. See docs/spec/05-screens-and-flows.md (stub tree).
/// </summary>
public static class GroupTree
{
    public static GroupNode Build(IReadOnlyList<Group> groups, IReadOnlyList<Stub> stubs)
        => BuildNode(null, groups, stubs);

    private static GroupNode BuildNode(Group? group, IReadOnlyList<Group> groups, IReadOnlyList<Stub> stubs)
    {
        var id = group?.Id;

        var children = groups
            .Where(g => g.ParentGroupId == id)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => BuildNode(g, groups, stubs))
            .ToList();

        var nodeStubs = stubs
            .Where(s => s.GroupId == id)
            // By the label the tree shows, so unnamed stubs sort by method and path rather than
            // clumping together under an empty name.
            .OrderBy(StubLabel.For, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GroupNode(group, children, nodeStubs);
    }
}
