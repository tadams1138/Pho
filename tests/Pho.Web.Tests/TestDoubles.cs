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

/// <summary>In-memory received-request log for mock-serving tests.</summary>
internal sealed class FakeReceivedRequestLog : IReceivedRequestLog
{
    public List<ReceivedRequest> Records { get; } = new();

    public Task RecordAsync(ReceivedRequest request) { Records.Add(request); return Task.CompletedTask; }

    public Task<PagedResult<ReceivedRequest>> QueryAsync(string? method, string? pathContains, int page, int pageSize)
    {
        var items = Records.OrderByDescending(r => r.ReceivedAt).ToList();
        return Task.FromResult(new PagedResult<ReceivedRequest>(items, items.Count, page, pageSize));
    }

    public Task ClearAsync() { Records.Clear(); return Task.CompletedTask; }
}

/// <summary>No-op config porter for admin-UI tests.</summary>
internal sealed class FakeConfigPorter : IConfigPorter
{
    public Task<string> ExportJsonAsync() => Task.FromResult("""{"Stubs":[],"Groups":[]}""");
    public Task ImportJsonAsync(string json, ImportMode mode) => Task.CompletedTask;
}

/// <summary>No-op config history store for admin-UI tests.</summary>
internal sealed class FakeConfigHistoryStore : IConfigHistoryStore
{
    public Task<int> GetCurrentSequenceAsync() => Task.FromResult(0);
    public Task SetCurrentSequenceAsync(int sequence) => Task.CompletedTask;
    public Task<ConfigSnapshot> CaptureAsync() => Task.FromResult(ConfigSnapshot.Empty);
    public Task RestoreAsync(ConfigSnapshot snapshot) => Task.CompletedTask;
    public Task AppendRevisionAsync(int sequence, string summary, ConfigSnapshot snapshot) => Task.CompletedTask;
    public Task TruncateAfterAsync(int sequence) => Task.CompletedTask;
    public Task<ConfigSnapshot?> GetRevisionSnapshotAsync(int sequence) => Task.FromResult<ConfigSnapshot?>(null);
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
