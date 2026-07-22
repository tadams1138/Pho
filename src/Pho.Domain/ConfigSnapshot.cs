using System.Collections.Generic;

namespace Pho.Domain;

/// <summary>
/// A point-in-time snapshot of the entire mock configuration — all stubs and all groups.
/// Config history versions these; undo/redo restore them. See docs/spec/03-domain-model.md
/// (Configuration history and undo/redo).
/// </summary>
public sealed record ConfigSnapshot(IReadOnlyList<Stub> Stubs, IReadOnlyList<Group> Groups)
{
    public static ConfigSnapshot Empty { get; } =
        new(new List<Stub>(), new List<Group>());
}
