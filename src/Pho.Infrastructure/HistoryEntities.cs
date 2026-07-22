using System;

namespace Pho.Infrastructure;

/// <summary>Persisted configuration revision: a whole-config snapshot as JSON, ordered by sequence.</summary>
public sealed class ConfigRevisionRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Sequence { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
}

/// <summary>Single-row table holding the current position in the configuration history.</summary>
public sealed class HistoryState
{
    public int Id { get; set; } = 1;
    public int CurrentSequence { get; set; }
}
