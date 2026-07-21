using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>EF Core/SQLite-backed <see cref="IStubRepository"/>.</summary>
public sealed class EfStubRepository : IStubRepository
{
    private readonly PhoDbContext _context;

    public EfStubRepository(PhoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Stub>> ListAsync()
        => await _context.Stubs.AsNoTracking().ToListAsync();

    public async Task<Stub?> GetAsync(Guid id)
        => await _context.Stubs.FindAsync(id);

    public async Task AddAsync(Stub stub)
    {
        _context.Stubs.Add(stub);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Stub stub)
    {
        _context.Stubs.Update(stub);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var stub = await _context.Stubs.FindAsync(id);
        if (stub is null) return;

        _context.Stubs.Remove(stub);
        await _context.SaveChangesAsync();
    }
}
