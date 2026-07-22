using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>Persistence operations for groups.</summary>
public interface IGroupRepository
{
    Task<IReadOnlyList<Group>> ListAsync();
    Task<Group?> GetAsync(Guid id);
    Task AddAsync(Group group);
    Task UpdateAsync(Group group);
    Task DeleteAsync(Guid id);
}
