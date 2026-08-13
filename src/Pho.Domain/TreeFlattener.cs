using System;
using System.Collections.Generic;

namespace Pho.Domain;

/// <summary>
/// Flattens the group tree into the ordered list of rows the UI actually shows, honouring
/// collapsed groups. Keyboard navigation and shift-range selection both work over this list,
/// so the ordering here is the tree's visual ordering. See docs/spec/05-screens-and-flows.md.
/// </summary>
public static class TreeFlattener
{
    private static readonly IReadOnlySet<Guid> None = new HashSet<Guid>();

    public static IReadOnlyList<TreeRow> Flatten(GroupNode root, IReadOnlySet<Guid>? collapsedGroupIds = null)
    {
        var rows = new List<TreeRow>();
        Walk(root, depth: 0, collapsedGroupIds ?? None, rows);
        return rows;
    }

    private static void Walk(GroupNode node, int depth, IReadOnlySet<Guid> collapsed, List<TreeRow> rows)
    {
        // Groups first, then stubs, matching GroupTree's ordering within a level.
        foreach (var child in node.Children)
        {
            var group = child.Group!;
            var expanded = !collapsed.Contains(group.Id);

            rows.Add(new TreeRow
            {
                Kind = TreeRowKind.Group,
                Id = group.Id,
                Depth = depth,
                ParentGroupId = group.ParentGroupId,
                Group = group,
                HasChildren = child.Children.Count > 0 || child.Stubs.Count > 0,
                Expanded = expanded
            });

            if (expanded) Walk(child, depth + 1, collapsed, rows);
        }

        foreach (var stub in node.Stubs)
        {
            rows.Add(new TreeRow
            {
                Kind = TreeRowKind.Stub,
                Id = stub.Id,
                Depth = depth,
                ParentGroupId = stub.GroupId,
                Stub = stub
            });
        }
    }
}
