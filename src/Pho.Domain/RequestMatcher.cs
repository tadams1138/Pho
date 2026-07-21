using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

public enum HttpMethodMatch
{
    Any,
    Get,
    Post,
    Put,
    Patch,
    Delete,
    Head,
    Options
}

/// <summary>A named parameter rule for a query parameter or header.</summary>
public sealed record ParamMatcher(string Name, MatchRule Rule);

/// <summary>
/// Describes which incoming requests a stub applies to. A request matches only when all
/// specified criteria match; unspecified criteria do not constrain.
/// See docs/spec/03-domain-model.md (RequestMatcher).
/// </summary>
public sealed record RequestMatcher
{
    public HttpMethodMatch Method { get; init; } = HttpMethodMatch.Any;
    public required PathMatcher Path { get; init; }
    public IReadOnlyList<ParamMatcher> QueryParams { get; init; } = new List<ParamMatcher>();
    public IReadOnlyList<ParamMatcher> Headers { get; init; } = new List<ParamMatcher>();
    public MatchRule? Body { get; init; }

    public bool Matches(HttpRequestData request)
    {
        if (!MethodMatches(request.Method)) return false;
        if (!Path.Matches(request.Path)) return false;

        foreach (var rule in QueryParams)
        {
            if (!rule.Rule.Matches(Lookup(request.QueryOrEmpty, rule.Name, ignoreCase: false))) return false;
        }

        foreach (var rule in Headers)
        {
            if (!rule.Rule.Matches(Lookup(request.HeadersOrEmpty, rule.Name, ignoreCase: true))) return false;
        }

        if (Body is not null && !Body.Matches(request.Body)) return false;

        return true;
    }

    private bool MethodMatches(string requestMethod)
        => Method == HttpMethodMatch.Any
           || string.Equals(Method.ToString(), requestMethod, System.StringComparison.OrdinalIgnoreCase);

    private static string? Lookup(IReadOnlyDictionary<string, string?> values, string name, bool ignoreCase)
    {
        if (values.TryGetValue(name, out var exact)) return exact;
        if (!ignoreCase) return null;

        return values.FirstOrDefault(kv => string.Equals(kv.Key, name, System.StringComparison.OrdinalIgnoreCase)).Value;
    }
}
