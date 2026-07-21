using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>
/// Persistence operations for stubs used by the authoring side (in-process, UI-only in v1).
/// </summary>
public interface IStubRepository
{
    Task<IReadOnlyList<Stub>> ListAsync();
    Task<Stub?> GetAsync(Guid id);
    Task AddAsync(Stub stub);
    Task UpdateAsync(Stub stub);
    Task DeleteAsync(Guid id);
}
