using System.Collections.Generic;

namespace Pho.Domain;

/// <summary>
/// A point-in-time snapshot of the entire mock configuration — all stubs and all groups.
/// Config history versions these; undo/redo restore them. See SPEC.md section 5.1 and 5.8
/// (the configuration and its history).
/// </summary>
public sealed record ConfigSnapshot(IReadOnlyList<Stub> Stubs, IReadOnlyList<Group> Groups)
{
    public static ConfigSnapshot Empty { get; } =
        new(new List<Stub>(), new List<Group>());
}
