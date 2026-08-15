namespace Pho.Domain;

/// <summary>
/// How a stub is labelled in the tree. A stub shows its own name when it has one; an unnamed stub
/// falls back to what it actually matches — "GET /users/1" — so no row is ever blank. Kept out of
/// <see cref="Stub"/> itself because Stub is serialized (export, config history) and a computed
/// member would leak into that shape. See docs/spec/05-screens-and-flows.md (stub tree).
/// </summary>
public static class StubLabel
{
    public static bool HasName(Stub stub) => !string.IsNullOrWhiteSpace(stub.Name);

    public static string For(Stub stub)
        => HasName(stub) ? stub.Name : ForRequest(stub.Request.Method, stub.Request.Path.Value);

    /// <summary>The label a request matcher produces on its own — also the default name a new stub is saved under.</summary>
    public static string ForRequest(HttpMethodMatch method, string path)
    {
        var verb = method.ToString().ToUpperInvariant();

        return string.IsNullOrWhiteSpace(path) ? verb : $"{verb} {path.Trim()}";
    }
}
