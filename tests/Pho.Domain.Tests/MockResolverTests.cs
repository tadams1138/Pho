using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class MockResolverTests
{
    private static Stub Stub(string path, bool enabled = true, string name = "stub")
        => new()
        {
            Name = name,
            Enabled = enabled,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, path) },
            Response = new ResponseDefinition { Status = 200 }
        };

    private static HttpRequestData Get(string path) => new("GET", path);

    [Fact]
    public void No_stub_matches_yields_NoMatch()
    {
        var result = MockResolver.Resolve(new[] { Stub("/a") }, Get("/b"));

        result.Outcome.Should().Be(MatchOutcome.NoMatch);
        result.MatchedStubs.Should().BeEmpty();
    }

    [Fact]
    public void Exactly_one_match_yields_MatchedOne()
    {
        var stub = Stub("/a");

        var result = MockResolver.Resolve(new[] { stub, Stub("/b") }, Get("/a"));

        result.Outcome.Should().Be(MatchOutcome.MatchedOne);
        result.Single.Should().BeSameAs(stub);
    }

    [Fact]
    public void Disabled_stub_is_ignored_even_if_it_would_match()
    {
        var result = MockResolver.Resolve(new[] { Stub("/a", enabled: false) }, Get("/a"));

        result.Outcome.Should().Be(MatchOutcome.NoMatch);
    }

    [Fact]
    public void Multiple_enabled_matches_yield_Ambiguous_with_all_matched_stubs()
    {
        var one = Stub("/a", name: "first");
        var two = Stub("/a", name: "second");

        var result = MockResolver.Resolve(new[] { one, two }, Get("/a"));

        result.Outcome.Should().Be(MatchOutcome.Ambiguous);
        result.MatchedStubs.Should().BeEquivalentTo(new[] { one, two });
    }

    [Fact]
    public void A_disabled_overlapping_stub_avoids_ambiguity()
    {
        var enabled = Stub("/a", name: "on");
        var disabled = Stub("/a", enabled: false, name: "off");

        var result = MockResolver.Resolve(new[] { enabled, disabled }, Get("/a"));

        result.Outcome.Should().Be(MatchOutcome.MatchedOne);
        result.Single.Should().BeSameAs(enabled);
    }
}
