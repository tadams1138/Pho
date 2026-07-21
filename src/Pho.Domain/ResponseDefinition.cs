using System.Collections.Generic;

namespace Pho.Domain;

public sealed record HeaderValue(string Name, string Value);

/// <summary>
/// What Pho returns when a stub is selected. See docs/spec/03-domain-model.md (ResponseDefinition).
/// </summary>
public sealed record ResponseDefinition
{
    public int Status { get; init; } = 200;
    public IReadOnlyList<HeaderValue> Headers { get; init; } = new List<HeaderValue>();
    public string Body { get; init; } = string.Empty;
}
