using System;
using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

public enum MatchOutcome
{
    MatchedOne,
    NoMatch,
    Ambiguous
}

/// <summary>
/// The result of resolving a request against the configured stubs.
/// </summary>
public sealed record MatchResult(MatchOutcome Outcome, IReadOnlyList<Stub> MatchedStubs)
{
    public static readonly MatchResult None = new(MatchOutcome.NoMatch, Array.Empty<Stub>());
    public static MatchResult One(Stub stub) => new(MatchOutcome.MatchedOne, new[] { stub });
    public static MatchResult Many(IReadOnlyList<Stub> stubs) => new(MatchOutcome.Ambiguous, stubs);

    public Stub? Single => Outcome == MatchOutcome.MatchedOne ? MatchedStubs[0] : null;
}

/// <summary>
/// Resolves an incoming request against the enabled stubs. There is no priority: exactly one
/// match is served, none is a 404, and more than one is an ambiguous-match error (HTTP 500).
/// See docs/spec/03-domain-model.md (Matching resolution) and F4 in docs/spec/04-features.md.
/// </summary>
public static class MockResolver
{
    public static MatchResult Resolve(IEnumerable<Stub> stubs, HttpRequestData request)
    {
        var matched = stubs.Where(s => s.Enabled && s.Request.Matches(request)).ToList();

        return matched.Count switch
        {
            0 => MatchResult.None,
            1 => MatchResult.One(matched[0]),
            _ => MatchResult.Many(matched)
        };
    }
}
