using System;
using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

/// <summary>
/// What a multi-row delete will actually remove. <see cref="GroupIds"/> / <see cref="StubIds"/> are
/// the rows to hand to the services — descendants covered by a selected group are left out because
/// group deletion already cascades — while the counts describe everything that disappears, which is
/// what the confirmation prompt shows.
/// </summary>
public sealed record DeletionPlan(
    IReadOnlyList<Guid> GroupIds,
    IReadOnlyList<Guid> StubIds,
    int GroupCount,
    int StubCount)
{
    public static readonly DeletionPlan Empty = new(Array.Empty<Guid>(), Array.Empty<Guid>(), 0, 0);

    public bool IsEmpty => GroupCount == 0 && StubCount == 0;

    /// <summary>e.g. "2 groups and 1 stub" — for the delete confirmation and the history summary.</summary>
    public string Describe()
    {
        var parts = new List<string>(2);
        if (GroupCount > 0) parts.Add(Plural(GroupCount, "group"));
        if (StubCount > 0) parts.Add(Plural(StubCount, "stub"));

        return parts.Count == 0 ? "nothing" : string.Join(" and ", parts);
    }

    private static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";
}

/// <summary>
/// Turns a multi-row selection into a <see cref="DeletionPlan"/>. Selecting a group implies
/// everything nested inside it (deletion cascades — see docs/spec/03-domain-model.md).
/// </summary>
public static class TreeDeletion
{
    public static DeletionPlan Plan(GroupNode root, IEnumerable<Guid> selectedIds)
    {
        var selected = selectedIds as ISet<Guid> ?? selectedIds.ToHashSet();
        if (selected.Count == 0) return DeletionPlan.Empty;

        var groupIds = new List<Guid>();
        var stubIds = new List<Guid>();
        var groupCount = 0;
        var stubCount = 0;

        void Walk(GroupNode node, bool insideDeleted)
        {
            foreach (var child in node.Children)
            {
                var id = child.Group!.Id;
                var deleted = insideDeleted || selected.Contains(id);

                if (deleted)
                {
                    groupCount++;
                    if (!insideDeleted) groupIds.Add(id);
                }

                Walk(child, deleted);
            }

            foreach (var stub in node.Stubs)
            {
                if (insideDeleted)
                {
                    stubCount++;
                }
                else if (selected.Contains(stub.Id))
                {
                    stubCount++;
                    stubIds.Add(stub.Id);
                }
            }
        }

        Walk(root, insideDeleted: false);

        return new DeletionPlan(groupIds, stubIds, groupCount, stubCount);
    }
}
