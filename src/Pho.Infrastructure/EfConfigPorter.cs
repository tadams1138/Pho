using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>
/// EF Core/SQLite-backed export/import. Export serializes the whole configuration (stubs + group
/// tree) as JSON; import applies it as replace-all or merge. See F8 in docs/spec/04-features.md.
/// </summary>
public sealed class EfConfigPorter : IConfigPorter
{
    private readonly PhoDbContext _context;

    public EfConfigPorter(PhoDbContext context)
    {
        _context = context;
    }

    public async Task<string> ExportJsonAsync()
    {
        var stubs = await _context.Stubs.AsNoTracking().ToListAsync();
        var groups = await _context.Groups.AsNoTracking().ToListAsync();
        return JsonSerializer.Serialize(new ConfigSnapshot(stubs, groups), PhoDbContext.JsonOptions);
    }

    public async Task ImportJsonAsync(string json, ImportMode mode)
    {
        var snapshot = Parse(json);

        if (mode == ImportMode.ReplaceAll)
        {
            await ReplaceAllAsync(snapshot);
        }
        else
        {
            await MergeAsync(snapshot);
        }
    }

    private static ConfigSnapshot Parse(string json)
    {
        ConfigSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ConfigSnapshot>(json, PhoDbContext.JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The file is not valid JSON.", ex);
        }

        if (snapshot is null || snapshot.Stubs is null || snapshot.Groups is null)
            throw new InvalidOperationException("The file is not a valid Pho export.");

        return snapshot;
    }

    private async Task ReplaceAllAsync(ConfigSnapshot snapshot)
    {
        _context.ChangeTracker.Clear();
        await _context.Stubs.ExecuteDeleteAsync();
        await _context.Groups.ExecuteDeleteAsync();

        _context.Groups.AddRange(snapshot.Groups);
        _context.Stubs.AddRange(snapshot.Stubs);
        await _context.SaveChangesAsync();
    }

    private async Task MergeAsync(ConfigSnapshot snapshot)
    {
        _context.ChangeTracker.Clear();

        foreach (var group in snapshot.Groups)
        {
            var existing = await _context.Groups.FindAsync(group.Id);
            if (existing is null)
            {
                _context.Groups.Add(group);
            }
            else
            {
                existing.Name = group.Name;
                existing.ParentGroupId = group.ParentGroupId;
            }
        }

        foreach (var stub in snapshot.Stubs)
        {
            var existing = await _context.Stubs.FindAsync(stub.Id);
            if (existing is null)
            {
                _context.Stubs.Add(stub);
            }
            else
            {
                existing.Name = stub.Name;
                existing.Description = stub.Description;
                existing.GroupId = stub.GroupId;
                existing.Enabled = stub.Enabled;
                existing.Request = stub.Request;
                existing.Response = stub.Response;
            }
        }

        await _context.SaveChangesAsync();
    }
}
