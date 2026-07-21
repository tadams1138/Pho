using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class MockServerTests
{
    private static Stub Stub(string path, int status, string body, bool enabled = true, string name = "stub")
        => new()
        {
            Name = name,
            Enabled = enabled,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, path) },
            Response = new ResponseDefinition
            {
                Status = status,
                Body = body,
                Headers = new List<HeaderValue> { new("Content-Type", "application/json") }
            }
        };

    private static HttpRequestData Get(string path) => new("GET", path);

    [Fact]
    public void Matched_request_returns_the_stub_response()
    {
        var stub = Stub("/a", 201, "{\"ok\":true}");

        var handling = MockServer.Handle(new[] { stub }, Get("/a"));

        handling.Match.Outcome.Should().Be(MatchOutcome.MatchedOne);
        handling.Response.Status.Should().Be(201);
        handling.Response.Body.Should().Be("{\"ok\":true}");
        handling.Response.Headers.Should().ContainSingle(h => h.Name == "Content-Type" && h.Value == "application/json");
    }

    [Fact]
    public void Unmatched_request_returns_404()
    {
        var handling = MockServer.Handle(new[] { Stub("/a", 200, "x") }, Get("/b"));

        handling.Match.Outcome.Should().Be(MatchOutcome.NoMatch);
        handling.Response.Status.Should().Be(404);
    }

    [Fact]
    public void Ambiguous_match_returns_500_naming_the_stubs()
    {
        var one = Stub("/a", 200, "x", name: "alpha");
        var two = Stub("/a", 200, "y", name: "beta");

        var handling = MockServer.Handle(new[] { one, two }, Get("/a"));

        handling.Match.Outcome.Should().Be(MatchOutcome.Ambiguous);
        handling.Response.Status.Should().Be(500);
        handling.Response.Body.Should().Contain("alpha").And.Contain("beta");
    }
}
