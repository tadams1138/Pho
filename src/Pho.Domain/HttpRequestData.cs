using System.Collections.Generic;

namespace Pho.Domain;

/// <summary>
/// A domain-level view of an inbound request on the mock-serving surface — the data used
/// for matching and recorded as a ReceivedRequest. See docs/spec/03-domain-model.md.
/// </summary>
public sealed record HttpRequestData(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string?>? Query = null,
    IReadOnlyDictionary<string, string?>? Headers = null,
    string? Body = null)
{
    public IReadOnlyDictionary<string, string?> QueryOrEmpty => Query ?? Empty;
    public IReadOnlyDictionary<string, string?> HeadersOrEmpty => Headers ?? Empty;

    private static readonly IReadOnlyDictionary<string, string?> Empty = new Dictionary<string, string?>();
}
