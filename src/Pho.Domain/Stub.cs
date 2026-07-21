using System;

namespace Pho.Domain;

/// <summary>
/// One mocking rule: a request matcher paired with a response. See docs/spec/03-domain-model.md (Stub).
/// Stubs have no priority; overlapping enabled stubs are an ambiguous-match error (see MockResolver).
/// </summary>
public sealed class Stub
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? GroupId { get; set; }
    public bool Enabled { get; set; } = true;
    public required RequestMatcher Request { get; set; }
    public required ResponseDefinition Response { get; set; }

    /// <summary>
    /// Creates a copy of this stub's definition with a new id, a "Copy of …" name, and
    /// <see cref="Enabled"/> set to false — so it does not immediately collide with the
    /// original (stubs have no priority; overlapping enabled stubs are an error). See F1.
    /// The request/response are immutable records and are safely shared by reference.
    /// </summary>
    public Stub Duplicate() => new()
    {
        Name = $"Copy of {Name}",
        Description = Description,
        GroupId = GroupId,
        Enabled = false,
        Request = Request,
        Response = Response
    };
}

