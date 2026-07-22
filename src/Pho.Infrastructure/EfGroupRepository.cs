using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>EF Core/SQLite-backed <see cref="IGroupRepository"/>.</summary>
public sealed class EfGroupRepository : IGroupRepository
{
    private readonly PhoDbContext _context;

    public EfGroupRepository(PhoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Group>> ListAsync()
        => await _context.Groups.AsNoTracking().ToListAsync();

    public async Task<Group?> GetAsync(Guid id)
        => await _context.Groups.FindAsync(id);

    public async Task AddAsync(Group group)
    {
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Group group)
    {
        _context.Groups.Update(group);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group is null) return;

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
    }
}
