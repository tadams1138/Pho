using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class MatchRuleTests
{
    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData(null, "abc", false)]
    public void Equals_rule(string? actual, string value, bool expected)
    {
        new MatchRule(MatchRuleType.Equals, value).Matches(actual).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello world", "world", true)]
    [InlineData("hello world", "mars", false)]
    [InlineData(null, "world", false)]
    public void Contains_rule(string? actual, string value, bool expected)
    {
        new MatchRule(MatchRuleType.Contains, value).Matches(actual).Should().Be(expected);
    }

    [Theory]
    [InlineData("12345", @"^\d+$", true)]
    [InlineData("12a45", @"^\d+$", false)]
    [InlineData(null, @"^\d+$", false)]
    public void Regex_rule(string? actual, string value, bool expected)
    {
        new MatchRule(MatchRuleType.Regex, value).Matches(actual).Should().Be(expected);
    }

    [Theory]
    [InlineData("anything", true)]
    [InlineData("", true)]
    [InlineData(null, false)]
    public void Present_rule(string? actual, bool expected)
    {
        new MatchRule(MatchRuleType.Present).Matches(actual).Should().Be(expected);
    }

    [Theory]
    [InlineData("anything", false)]
    [InlineData(null, true)]
    public void Absent_rule(string? actual, bool expected)
    {
        new MatchRule(MatchRuleType.Absent).Matches(actual).Should().Be(expected);
    }
}
