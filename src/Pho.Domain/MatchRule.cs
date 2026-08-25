using System.Text.RegularExpressions;

namespace Pho.Domain;

public enum MatchRuleType
{
    Equals,
    Contains,
    Regex,
    Present,
    Absent
}

/// <summary>
/// A rule applied to a single value (query param, header, or body).
/// See docs/spec/03-domain-model.md (MatchRule).
/// </summary>
/// <param name="IgnoreCase">
/// Opts this rule's <em>value</em> comparison out of case sensitivity. Off by default: header values
/// are not case-insensitive in general, and some carry case-significant payloads (a base64 Basic
/// credential among them), so a mock that folded case everywhere would accept what the real service
/// rejects. Applies to EQUALS and CONTAINS only — REGEX states its own case-insensitivity inline
/// with <c>(?i)</c>, and PRESENT / ABSENT compare no value. Header <em>names</em> are a separate
/// question, always compared case-insensitively (see <see cref="RequestMatcher"/>).
/// </param>
public sealed record MatchRule(MatchRuleType Type, string? Value = null, bool IgnoreCase = false)
{
    public bool Matches(string? actual)
    {
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return Type switch
        {
            MatchRuleType.Equals => actual is not null && string.Equals(actual, Value, comparison),
            MatchRuleType.Contains => actual is not null && Value is not null && actual.Contains(Value, comparison),
            MatchRuleType.Regex => actual is not null && Value is not null && Regex.IsMatch(actual, Value),
            MatchRuleType.Present => actual is not null,
            MatchRuleType.Absent => actual is null,
            _ => false
        };
    }
}
