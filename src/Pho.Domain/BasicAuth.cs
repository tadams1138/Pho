using System;
using System.Text;

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

    /// <summary>Builds the full header value, e.g. <c>Basic dXNlcjpwYXNz</c>.</summary>
    public static string Encode(string userId, string password)
    {
        var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userId}:{password}"));

        return $"{Scheme} {credential}";
    }

    /// <summary>
    /// The credential a header value carries, or null when it is not a decodable Basic credential —
    /// another scheme, invalid base64, or decoded text with no colon to split on.
    /// </summary>
    public static BasicCredentials? Decode(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return null;

        var trimmed = headerValue.Trim();
        if (!trimmed.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;

        var credential = trimmed[Scheme.Length..].Trim();
        if (credential.Length == 0) return null;

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

    /// <summary>A human reading of an encoded credential — the editor's hover text — or null when it does not decode.</summary>
    public static string? Describe(string? headerValue)
    {
        if (Decode(headerValue) is not { } credentials) return null;

        var password = credentials.Password.Length == 0 ? "(empty)" : credentials.Password;

        return $"User ID: {credentials.UserId} · Password: {password}";
    }
}
