using System.Collections.Generic;

namespace Pho.Domain;

public sealed record HeaderValue(string Name, string Value);

/// <summary>
/// What Pho returns when a stub is selected. See SPEC.md section 5.6 (P-1 to P-4).
/// </summary>
public sealed record ResponseDefinition
{
    public int Status { get; init; } = 200;
    public IReadOnlyList<HeaderValue> Headers { get; init; } = new List<HeaderValue>();
    public string Body { get; init; } = string.Empty;
}
