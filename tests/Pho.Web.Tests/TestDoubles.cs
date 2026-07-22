using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Pho.Domain;
using Pho.Web;

namespace Pho.Web.Tests;

/// <summary>Forces every request to be treated as mock traffic (TestServer has no real ports).</summary>
internal sealed class AlwaysMockTrafficPolicy : IMockTrafficPolicy
{
    public bool IsMockTraffic(HttpContext context) => true;
}

/// <summary>In-memory stub repository for admin-UI tests, avoiding a real database.</summary>
internal sealed class FakeStubRepository : IStubRepository
{
    private readonly Dictionary<Guid, Stub> _stubs = new();

    public FakeStubRepository(IEnumerable<Stub>? seed = null)
    {
        if (seed is null) return;
        foreach (var stub in seed) _stubs[stub.Id] = stub;
    }

    public Task<IReadOnlyList<Stub>> ListAsync()
        => Task.FromResult<IReadOnlyList<Stub>>(_stubs.Values.ToList());

    public Task<Stub?> GetAsync(Guid id)
        => Task.FromResult(_stubs.GetValueOrDefault(id));

    public Task AddAsync(Stub stub) { _stubs[stub.Id] = stub; return Task.CompletedTask; }

    public Task UpdateAsync(Stub stub) { _stubs[stub.Id] = stub; return Task.CompletedTask; }

    public Task DeleteAsync(Guid id) { _stubs.Remove(id); return Task.CompletedTask; }
}

/// <summary>In-memory group repository for admin-UI tests.</summary>
internal sealed class FakeGroupRepository : IGroupRepository
{
    private readonly Dictionary<Guid, Group> _groups = new();

    public Task<IReadOnlyList<Group>> ListAsync()
        => Task.FromResult<IReadOnlyList<Group>>(_groups.Values.ToList());

    public Task<Group?> GetAsync(Guid id)
        => Task.FromResult(_groups.GetValueOrDefault(id));

    public Task AddAsync(Group group) { _groups[group.Id] = group; return Task.CompletedTask; }

    public Task UpdateAsync(Group group) { _groups[group.Id] = group; return Task.CompletedTask; }

    public Task DeleteAsync(Guid id) { _groups.Remove(id); return Task.CompletedTask; }
}
