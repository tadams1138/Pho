using System.Collections.Generic;
using System.Linq;
using Pho.Domain;

namespace Pho.Web;

/// <summary>
/// In-memory stub store for the mock-serving slice. Replaced by an EF Core/SQLite-backed
/// store when persistence is implemented.
/// </summary>
public sealed class InMemoryStubStore : IStubStore
{
    private readonly List<Stub> _stubs;

    public InMemoryStubStore(IEnumerable<Stub>? seed = null)
    {
        _stubs = seed?.ToList() ?? new List<Stub>();
    }

    public IReadOnlyList<Stub> GetAll() => _stubs;
}
