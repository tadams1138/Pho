using System;

namespace Pho.Domain;

/// <summary>
/// One mocking rule: a request matcher paired with a response. See docs/spec/03-domain-model.md (Stub).
/// Stubs have no priority; overlapping enabled stubs are an ambiguous-match error (see MockResolver).
/// </summary>
public sealed class Stub
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? GroupId { get; set; }
    public bool Enabled { get; set; } = true;
    public required RequestMatcher Request { get; set; }
    public required ResponseDefinition Response { get; set; }
}
