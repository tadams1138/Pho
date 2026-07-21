using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class PathMatcherTests
{
    [Fact]
    public void Exact_matches_identical_path()
    {
        var matcher = new PathMatcher(PathMatchType.Exact, "/users/123");

        matcher.Matches("/users/123").Should().BeTrue();
    }

    [Fact]
    public void Exact_does_not_match_different_path()
    {
        var matcher = new PathMatcher(PathMatchType.Exact, "/users/123");

        matcher.Matches("/users/999").Should().BeFalse();
    }

    [Theory]
    [InlineData("/users/*", "/users/123", true)]
    [InlineData("/users/*", "/users/abc", true)]
    [InlineData("/users/*", "/users", false)]
    [InlineData("/users/*", "/users/123/orders", false)]
    [InlineData("/users/{id}", "/users/123", true)]
    [InlineData("/users/{id}/orders", "/users/7/orders", true)]
    public void Wildcard_matches_a_single_segment(string pattern, string path, bool expected)
    {
        var matcher = new PathMatcher(PathMatchType.Wildcard, pattern);

        matcher.Matches(path).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"^/users/\d+$", "/users/123", true)]
    [InlineData(@"^/users/\d+$", "/users/abc", false)]
    public void Regex_matches_the_pattern(string pattern, string path, bool expected)
    {
        var matcher = new PathMatcher(PathMatchType.Regex, pattern);

        matcher.Matches(path).Should().Be(expected);
    }
}
