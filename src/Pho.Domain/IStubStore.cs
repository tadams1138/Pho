using System.Collections.Generic;

namespace Pho.Domain;

/// <summary>
/// Read access to the currently configured stubs, used by the mock-serving surface.
/// Backed in-memory today; by EF Core/SQLite once persistence lands.
/// </summary>
public interface IStubStore
{
    IReadOnlyList<Stub> GetAll();
}
