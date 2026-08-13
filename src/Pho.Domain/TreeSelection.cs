using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Pho.Domain;

/// <summary>
/// The set of tree rows the user has selected, plus the anchor a shift-range extends from.
/// Immutable: every operation returns a new selection. Groups and stubs share one id space,
/// so a selection can mix both. See docs/spec/05-screens-and-flows.md (stub tree).
/// </summary>
public sealed class TreeSelection
{
    public static readonly TreeSelection Empty = new(ImmutableHashSet<Guid>.Empty, null);

    private readonly ImmutableHashSet<Guid> _ids;

    private TreeSelection(ImmutableHashSet<Guid> ids, Guid? anchor)
    {
        _ids = ids;
        Anchor = anchor;
    }

    /// <summary>The row a shift-click or shift-arrow extends the range from.</summary>
    public Guid? Anchor { get; }

    public IReadOnlyCollection<Guid> Ids => _ids;

    public int Count => _ids.Count;

    public bool IsEmpty => _ids.IsEmpty;

    /// <summary>The only selected row, or null when zero or several rows are selected.</summary>
    public Guid? SingleId => _ids.Count == 1 ? _ids.First() : null;

    public bool Contains(Guid id) => _ids.Contains(id);

    /// <summary>Plain click: this row alone, and it becomes the anchor.</summary>
    public TreeSelection SelectOnly(Guid id) => new(ImmutableHashSet.Create(id), id);

    /// <summary>Ctrl/cmd click: add or remove one row without disturbing the rest.</summary>
    public TreeSelection Toggle(Guid id)
        => new(_ids.Contains(id) ? _ids.Remove(id) : _ids.Add(id), id);

    /// <summary>
    /// Shift click: select every row between the anchor and <paramref name="targetId"/> inclusive,
    /// replacing the previous selection. Without a usable anchor this behaves like a plain click.
    /// </summary>
    public TreeSelection ExtendTo(IReadOnlyList<TreeRow> rows, Guid targetId)
    {
        var target = IndexOf(rows, targetId);
        var anchor = Anchor is Guid a ? IndexOf(rows, a) : -1;
        if (target < 0) return this;
        if (anchor < 0) return SelectOnly(targetId);

        var (from, to) = anchor <= target ? (anchor, target) : (target, anchor);
        var range = rows.Skip(from).Take(to - from + 1).Select(r => r.Id);

        return new TreeSelection(ImmutableHashSet.CreateRange(range), Anchor);
    }

    public TreeSelection Clear() => Empty;

    /// <summary>Keeps only rows that still exist — used after a reload, delete, or move.</summary>
    public TreeSelection Retain(IEnumerable<Guid> existingIds)
    {
        var existing = existingIds as ISet<Guid> ?? existingIds.ToHashSet();
        var kept = _ids.Where(existing.Contains).ToImmutableHashSet();
        var anchor = Anchor is Guid a && existing.Contains(a) ? a : (Guid?)null;

        return kept.IsEmpty && anchor is null ? Empty : new TreeSelection(kept, anchor);
    }

    private static int IndexOf(IReadOnlyList<TreeRow> rows, Guid id)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == id) return i;
        }

        return -1;
    }
}
