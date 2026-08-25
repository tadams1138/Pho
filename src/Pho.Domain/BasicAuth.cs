using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Pho.Domain;

/// <summary>The two halves of an HTTP Basic credential.</summary>
public sealed record BasicCredentials(string UserId, string Password);

/// <summary>
/// Encodes and decodes HTTP Basic credentials for the stub editor's auth helper (F12). Mocked
/// services are routinely Basic-protected, and the header value is a base64 blob — this turns a
/// user id and password into that blob, and reads one back, so a test author never encodes by hand.
/// </summary>
public static class BasicAuth
{
    /// <summary>The header a Basic credential belongs to.</summary>
    public const string HeaderName = "Authorization";

    private const string Scheme = "Basic";

    private const string PatternPrefix = @"^(?i:" + Scheme + @")\s+";
    private const string PatternSuffix = "$";

    /// <summary>Builds the full header value, e.g. <c>Basic dXNlcjpwYXNz</c>.</summary>
    public static string Encode(string userId, string password)
    {
        return $"{Scheme} {Credential(userId, password)}";
    }

    /// <summary>
    /// Builds the REGEX rule value the editor's helper writes: <c>^(?i:Basic)\s+dXNlcjpwYXNz$</c>.
    /// <para>
    /// The two halves of an Authorization value need opposite treatment, which no single ignore-case
    /// flag can express. An authentication scheme is a case-insensitive token (RFC 9110 §11.1), so a
    /// system under test may send <c>BASIC</c>; the credential after it is base64, whose alphabet is
    /// case-significant, so folding its case would accept passwords the stub was written to reject.
    /// A scoped inline option covers the scheme word alone. The pattern is anchored and spells the
    /// scheme out, so leniency extends to case and nothing else — <c>BASSIC</c> and <c>Bearer</c>
    /// both miss. The credential is escaped because base64 emits <c>+</c>, which would otherwise
    /// quantify the character before it.
    /// </para>
    /// </summary>
    public static string EncodePattern(string userId, string password)
    {
        return PatternPrefix + Regex.Escape(Credential(userId, password)) + PatternSuffix;
    }

    private static string Credential(string userId, string password)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userId}:{password}"));

    /// <summary>
    /// The credential a header value carries, or null when it is not a decodable Basic credential —
    /// another scheme, invalid base64, or decoded text with no colon to split on.
    /// <para>
    /// Reads both forms a rule may hold: the pattern <see cref="EncodePattern"/> builds, and a plain
    /// <c>Basic …</c> value typed by hand or imported from a configuration written before patterns
    /// existed. A regex of any other shape is left alone — it is an ordinary rule, not a credential.
    /// </para>
    /// </summary>
    public static BasicCredentials? Decode(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return null;

        var trimmed = headerValue.Trim();
        var credential = CredentialFromPattern(trimmed) ?? CredentialFromPlainValue(trimmed);
        if (credential is null) return null;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(credential));
        }
        catch (FormatException)
        {
            return null;
        }

        // Only the first colon separates the two: a password may contain colons, a user id may not.
        var separator = decoded.IndexOf(':');
        if (separator < 0) return null;

        return new BasicCredentials(decoded[..separator], decoded[(separator + 1)..]);
    }

    /// <summary>
    /// The base64 inside a pattern this class built, or null when the value is not that exact shape.
    /// The match is deliberately literal rather than a regex over a regex: only what the helper
    /// writes reads back as a credential, so an author's own pattern is never second-guessed.
    /// </summary>
    private static string? CredentialFromPattern(string value)
    {
        if (!value.StartsWith(PatternPrefix, StringComparison.Ordinal)) return null;
        if (!value.EndsWith(PatternSuffix, StringComparison.Ordinal)) return null;

        var escaped = value[PatternPrefix.Length..^PatternSuffix.Length];
        if (escaped.Length == 0) return null;

        try
        {
            return Regex.Unescape(escaped);
        }
        catch (ArgumentException)
        {
            return null;   // a malformed escape is not a credential
        }
    }

    /// <summary>The base64 after a bare <c>Basic</c> scheme word, or null when there is none.</summary>
    private static string? CredentialFromPlainValue(string value)
    {
        if (!value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;

        var credential = value[Scheme.Length..].Trim();

        return credential.Length == 0 ? null : credential;
    }

    /// <summary>A human reading of an encoded credential — the editor's hover text — or null when it does not decode.</summary>
    public static string? Describe(string? headerValue)
    {
        if (Decode(headerValue) is not { } credentials) return null;

        var password = credentials.Password.Length == 0 ? "(empty)" : credentials.Password;

        return $"User ID: {credentials.UserId} · Password: {password}";
    }
}
