using System;
using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

/// <summary>
/// The stubs a multi-row selection covers: stubs picked directly, plus every stub nested inside a
/// selected group. Selecting a group means its contents here for the same reason it does when
/// deleting (see <see cref="TreeDeletion"/>) — it is what backs the toolbar's Enable / Disable
/// buttons, which act on the whole selection at once. See docs/spec/04-features.md (F1).
/// </summary>
public static class TreeStubSelection
{
    public static IReadOnlyList<Stub> Collect(GroupNode root, IEnumerable<Guid> selectedIds)
    {
        var selected = selectedIds as ISet<Guid> ?? selectedIds.ToHashSet();
        if (selected.Count == 0) return Array.Empty<Stub>();

        var covered = new List<Stub>();

        void Walk(GroupNode node, bool insideSelected)
        {
            foreach (var child in node.Children)
            {
                Walk(child, insideSelected || selected.Contains(child.Group!.Id));
            }

            foreach (var stub in node.Stubs)
            {
                if (insideSelected || selected.Contains(stub.Id)) covered.Add(stub);
            }
        }

        Walk(root, insideSelected: false);

        return covered;
    }
}
