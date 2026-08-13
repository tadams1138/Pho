using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>
/// Application service for managing stubs (F1/F2). In v1 this is called in-process by the
/// Blazor UI; there is no public authoring API.
/// </summary>
public sealed class StubService
{
    private readonly IStubRepository _repository;

    public StubService(IStubRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Stub>> ListAsync() => _repository.ListAsync();

    public Task<Stub?> GetAsync(Guid id) => _repository.GetAsync(id);

    public async Task<Stub> CreateAsync(Stub stub)
    {
        await _repository.AddAsync(stub);
        return stub;
    }

    public Task UpdateAsync(Stub stub) => _repository.UpdateAsync(stub);

    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public async Task<Stub> DuplicateAsync(Guid id)
    {
        var source = await _repository.GetAsync(id)
            ?? throw new InvalidOperationException($"Stub {id} not found.");
        var copy = source.Duplicate();
        await _repository.AddAsync(copy);
        return copy;
    }

    /// <summary>Moves a stub into another group, or to the tree root when <paramref name="groupId"/> is null.</summary>
    public async Task MoveAsync(Guid id, Guid? groupId)
    {
        var stub = await _repository.GetAsync(id)
            ?? throw new InvalidOperationException($"Stub {id} not found.");
        stub.GroupId = groupId;
        await _repository.UpdateAsync(stub);
    }

    public async Task SetEnabledAsync(Guid id, bool enabled)
    {
        var stub = await _repository.GetAsync(id)
            ?? throw new InvalidOperationException($"Stub {id} not found.");
        stub.Enabled = enabled;
        await _repository.UpdateAsync(stub);
    }
}
