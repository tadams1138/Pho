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
public sealed record MatchRule(MatchRuleType Type, string? Value = null)
{
    public bool Matches(string? actual)
    {
        return Type switch
        {
            MatchRuleType.Equals => actual is not null && string.Equals(actual, Value, StringComparison.Ordinal),
            MatchRuleType.Contains => actual is not null && Value is not null && actual.Contains(Value, StringComparison.Ordinal),
            MatchRuleType.Regex => actual is not null && Value is not null && Regex.IsMatch(actual, Value),
            MatchRuleType.Present => actual is not null,
            MatchRuleType.Absent => actual is null,
            _ => false
        };
    }
}
