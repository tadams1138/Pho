using System.Collections.Generic;
using System.Linq;

namespace Pho.Domain;

/// <summary>The concrete HTTP response Pho will send on the mock-serving surface.</summary>
public sealed record MockResponse(int Status, IReadOnlyList<HeaderValue> Headers, string Body);

/// <summary>The outcome of handling one request: the match result plus the response to send.</summary>
public sealed record MockHandling(MatchResult Match, MockResponse Response);

/// <summary>
/// Turns an incoming request into a response by resolving it against the stubs.
/// Exactly one match serves the stub's response; no match is 404; more than one is a
/// 500 ambiguous-match error naming the conflicting stubs (F4, no priority).
/// </summary>
public static class MockServer
{
    private static readonly IReadOnlyList<HeaderValue> PlainText =
        new List<HeaderValue> { new("Content-Type", "text/plain; charset=utf-8") };

    public static MockHandling Handle(IEnumerable<Stub> stubs, HttpRequestData request)
    {
        var match = MockResolver.Resolve(stubs, request);

        var response = match.Outcome switch
        {
            MatchOutcome.MatchedOne => ToResponse(match.Single!.Response),
            MatchOutcome.Ambiguous => Ambiguous(match.MatchedStubs),
            _ => NoMatch()
        };

        return new MockHandling(match, response);
    }

    private static MockResponse ToResponse(ResponseDefinition definition)
        => new(definition.Status, definition.Headers, definition.Body);

    private static MockResponse NoMatch()
        => new(404, PlainText, "No stub matched this request.");

    private static MockResponse Ambiguous(IReadOnlyList<Stub> matched)
    {
        var names = string.Join(", ", matched.Select(s => $"'{s.Name}' ({s.Id})"));
        return new MockResponse(500, PlainText,
            $"Ambiguous match: the request matched multiple stubs: {names}. " +
            "Disable or narrow all but one (stubs have no priority).");
    }
}
