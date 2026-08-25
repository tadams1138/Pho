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

    [Fact]
    public void A_rule_compares_case_sensitively_until_told_otherwise()
    {
        // Arrange
        var rule = new MatchRule(MatchRuleType.Equals, "application/json");

        // Act
        var matched = rule.Matches("application/JSON");

        // Assert
        rule.IgnoreCase.Should().BeFalse("case sensitivity is the default");
        matched.Should().BeFalse();
    }

    [Theory]
    [InlineData("application/JSON", true)]
    [InlineData("APPLICATION/JSON", true)]
    [InlineData("application/xml", false)]
    public void Equals_rule_ignoring_case(string actual, bool expected)
    {
        // Arrange
        var rule = new MatchRule(MatchRuleType.Equals, "application/json") { IgnoreCase = true };

        // Act
        var matched = rule.Matches(actual);

        // Assert
        matched.Should().Be(expected);
    }

    [Theory]
    [InlineData("charset=UTF-8", true)]
    [InlineData("CHARSET=utf-8", true)]
    [InlineData("charset=ascii", false)]
    public void Contains_rule_ignoring_case(string actual, bool expected)
    {
        // Arrange
        var rule = new MatchRule(MatchRuleType.Contains, "utf-8") { IgnoreCase = true };

        // Act
        var matched = rule.Matches(actual);

        // Assert
        matched.Should().Be(expected);
    }

    [Fact]
    public void A_regex_rule_ignores_the_flag_and_states_its_own_case_insensitivity()
    {
        // Arrange — the flag is set, but a regex says (?i) itself; two ways to say it would be a defect
        var flagged = new MatchRule(MatchRuleType.Regex, "^abc$") { IgnoreCase = true };
        var inline = new MatchRule(MatchRuleType.Regex, "^(?i)abc$");

        // Act
        var flaggedMatched = flagged.Matches("ABC");
        var inlineMatched = inline.Matches("ABC");

        // Assert
        flaggedMatched.Should().BeFalse("the flag does not apply to REGEX rules");
        inlineMatched.Should().BeTrue();
    }

    [Fact]
    public void Present_and_absent_rules_are_unaffected_by_the_flag()
    {
        // Arrange
        var present = new MatchRule(MatchRuleType.Present) { IgnoreCase = true };
        var absent = new MatchRule(MatchRuleType.Absent) { IgnoreCase = true };

        // Act / Assert — they compare no value, so there is no case to fold
        present.Matches("anything").Should().BeTrue();
        present.Matches(null).Should().BeFalse();
        absent.Matches(null).Should().BeTrue();
        absent.Matches("anything").Should().BeFalse();
    }
}
