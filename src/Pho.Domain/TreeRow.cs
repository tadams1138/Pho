using System;

namespace Pho.Domain;

/// <summary>What a <see cref="TreeRow"/> stands for.</summary>
public enum TreeRowKind
{
    Group,
    Stub
}

/// <summary>
/// One visible line of the stub tree, flattened into display order. The flat list is what the UI
/// renders, multi-selects, and moves through with the arrow keys — see
/// <see cref="TreeFlattener"/> and docs/spec/05-screens-and-flows.md (stub tree).
/// </summary>
public sealed record TreeRow
{
    public required TreeRowKind Kind { get; init; }

    /// <summary>The group id or stub id this row shows; unique across the whole tree.</summary>
    public required Guid Id { get; init; }

    /// <summary>Nesting level; root-level rows are 0.</summary>
    public required int Depth { get; init; }

    /// <summary>The containing group, or null when the row sits at the root.</summary>
    public Guid? ParentGroupId { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="TreeRowKind.Group"/>.</summary>
    public Group? Group { get; init; }

    /// <summary>Set when <see cref="Kind"/> is <see cref="TreeRowKind.Stub"/>.</summary>
    public Stub? Stub { get; init; }

    /// <summary>Groups only: whether the group holds any child groups or stubs.</summary>
    public bool HasChildren { get; init; }

    /// <summary>Groups only: whether the group's contents are currently shown.</summary>
    public bool Expanded { get; init; }

    public string Name => Kind == TreeRowKind.Group ? Group!.Name : Stub!.Name;

    /// <summary>The group a drop onto this row targets: the group itself, or a stub's group.</summary>
    public Guid? DropTargetGroupId => Kind == TreeRowKind.Group ? Group!.Id : ParentGroupId;
}
