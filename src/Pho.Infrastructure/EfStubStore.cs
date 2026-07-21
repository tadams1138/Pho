using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>
/// EF Core-backed read side of the stub store, used by the mock-serving surface.
/// </summary>
public sealed class EfStubStore : IStubStore
{
    private readonly PhoDbContext _context;

    public EfStubStore(PhoDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<Stub> GetAll() => _context.Stubs.AsNoTracking().ToList();
}
