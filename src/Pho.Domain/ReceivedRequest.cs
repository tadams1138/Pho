using System;
using System.Collections.Generic;

namespace Pho.Domain;

/// <summary>
/// An immutable record of one request received on the mock-serving surface, captured verbatim
/// for verification (F5). Recorded for every request — matched, unmatched, or ambiguous.
/// See docs/spec/03-domain-model.md (ReceivedRequest).
/// </summary>
public sealed class ReceivedRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime ReceivedAt { get; init; }
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Headers { get; init; } = new Dictionary<string, string?>();
    public string Body { get; init; } = string.Empty;
    public MatchOutcome Outcome { get; init; }
    public IReadOnlyList<Guid> MatchedStubIds { get; init; } = new List<Guid>();
    public int ResponseStatus { get; init; }
}
