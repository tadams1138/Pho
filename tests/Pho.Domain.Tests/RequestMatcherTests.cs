using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class RequestMatcherTests
{
    private static HttpRequestData Request(
        string method = "GET",
        string path = "/users/123",
        IReadOnlyDictionary<string, string?>? query = null,
        IReadOnlyDictionary<string, string?>? headers = null,
        string? body = null)
        => new(method, path, query, headers, body);

    private static RequestMatcher Matcher(
        HttpMethodMatch method = HttpMethodMatch.Get,
        PathMatcher? path = null,
        IReadOnlyList<ParamMatcher>? query = null,
        IReadOnlyList<ParamMatcher>? headers = null,
        MatchRule? body = null)
        => new()
        {
            Method = method,
            Path = path ?? new PathMatcher(PathMatchType.Exact, "/users/123"),
            QueryParams = query ?? new List<ParamMatcher>(),
            Headers = headers ?? new List<ParamMatcher>(),
            Body = body
        };

    [Fact]
    public void Matches_when_method_and_path_match()
    {
        Matcher().Matches(Request()).Should().BeTrue();
    }

    [Fact]
    public void Does_not_match_on_different_method()
    {
        Matcher(method: HttpMethodMatch.Get).Matches(Request(method: "POST")).Should().BeFalse();
    }

    [Fact]
    public void Any_method_matches_regardless_of_verb()
    {
        Matcher(method: HttpMethodMatch.Any).Matches(Request(method: "DELETE")).Should().BeTrue();
    }

    [Fact]
    public void Method_comparison_is_case_insensitive()
    {
        Matcher(method: HttpMethodMatch.Get).Matches(Request(method: "get")).Should().BeTrue();
    }

    [Fact]
    public void Does_not_match_on_different_path()
    {
        Matcher().Matches(Request(path: "/other")).Should().BeFalse();
    }

    [Fact]
    public void Requires_all_query_rules_to_match()
    {
        var matcher = Matcher(query: new List<ParamMatcher>
        {
            new("page", new MatchRule(MatchRuleType.Equals, "2"))
        });

        matcher.Matches(Request(query: new Dictionary<string, string?> { ["page"] = "2" })).Should().BeTrue();
        matcher.Matches(Request(query: new Dictionary<string, string?> { ["page"] = "3" })).Should().BeFalse();
    }

    [Fact]
    public void Header_lookup_is_case_insensitive_on_name()
    {
        var matcher = Matcher(headers: new List<ParamMatcher>
        {
            new("Authorization", new MatchRule(MatchRuleType.Present))
        });

        matcher.Matches(Request(headers: new Dictionary<string, string?> { ["authorization"] = "Bearer x" }))
            .Should().BeTrue();
    }

    [Fact]
    public void Requires_body_rule_to_match_when_specified()
    {
        var matcher = Matcher(method: HttpMethodMatch.Post, body: new MatchRule(MatchRuleType.Contains, "needle"));

        matcher.Matches(Request(method: "POST", body: "a needle here")).Should().BeTrue();
        matcher.Matches(Request(method: "POST", body: "nothing")).Should().BeFalse();
    }

    [Fact]
    public void Unspecified_criteria_do_not_constrain()
    {
        // No query/header/body rules: a request with extra query params still matches.
        Matcher().Matches(Request(query: new Dictionary<string, string?> { ["extra"] = "1" }))
            .Should().BeTrue();
    }
}
