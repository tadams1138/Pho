using System;

namespace Pho.Domain;

/// <summary>
/// An organizational folder for stubs. Groups form a tree via <see cref="ParentGroupId"/>.
/// Deleting a group cascades to all descendants (see GroupService). See SPEC.md G-5.
/// </summary>
public sealed class Group
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? ParentGroupId { get; set; }
}
