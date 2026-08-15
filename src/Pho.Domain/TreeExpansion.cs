using System;
using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

/// <summary>
/// Which groups an expand-all / collapse-all acts on. With nothing selected the action covers the
/// whole tree; with groups selected it is scoped to those groups and everything nested under them,
/// so a user can open or close one branch without disturbing the rest.
/// See docs/spec/05-screens-and-flows.md (stub tree).
/// </summary>
public static class TreeExpansion
{
    /// <summary>Every group in the tree — the collapse-all set, and the state the app starts in.</summary>
    public static IReadOnlyList<Guid> AllGroupIds(GroupNode root)
    {
        var ids = new List<Guid>();

        void Walk(GroupNode node)
        {
            foreach (var child in node.Children)
            {
                ids.Add(child.Group!.Id);
                Walk(child);
            }
        }

        Walk(root);

        return ids;
    }

    /// <summary>
    /// The groups the action applies to: every group when nothing is selected, otherwise the
    /// selected groups and their descendants (no groups selected scopes the action to nothing).
    /// </summary>
    public static IReadOnlyList<Guid> Scope(GroupNode root, IEnumerable<Guid> selectedIds)
    {
        var selected = selectedIds as ISet<Guid> ?? selectedIds.ToHashSet();
        if (selected.Count == 0) return AllGroupIds(root);

        var scoped = new List<Guid>();

        void Walk(GroupNode node, bool insideSelected)
        {
            foreach (var child in node.Children)
            {
                var id = child.Group!.Id;
                var inScope = insideSelected || selected.Contains(id);
                if (inScope) scoped.Add(id);

                Walk(child, inScope);
            }
        }

        Walk(root, insideSelected: false);

        return scoped;
    }
}
