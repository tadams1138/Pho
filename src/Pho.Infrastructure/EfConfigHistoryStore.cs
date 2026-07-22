using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>
/// EF Core/SQLite-backed configuration history. Snapshots are stored as JSON; restoring replaces
/// the live stubs and groups wholesale (preserving ids so references stay valid).
/// </summary>
public sealed class EfConfigHistoryStore : IConfigHistoryStore
{
    private readonly PhoDbContext _context;

    public EfConfigHistoryStore(PhoDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetCurrentSequenceAsync()
    {
        var state = await _context.HistoryState.AsNoTracking().FirstOrDefaultAsync();
        return state?.CurrentSequence ?? 0;
    }

    public async Task SetCurrentSequenceAsync(int sequence)
    {
        var state = await _context.HistoryState.FirstOrDefaultAsync();
        if (state is null)
        {
            _context.HistoryState.Add(new HistoryState { Id = 1, CurrentSequence = sequence });
        }
        else
        {
            state.CurrentSequence = sequence;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<ConfigSnapshot> CaptureAsync()
    {
        var stubs = await _context.Stubs.AsNoTracking().ToListAsync();
        var groups = await _context.Groups.AsNoTracking().ToListAsync();
        return new ConfigSnapshot(stubs, groups);
    }

    public async Task RestoreAsync(ConfigSnapshot snapshot)
    {
        _context.ChangeTracker.Clear();
        await _context.Stubs.ExecuteDeleteAsync();
        await _context.Groups.ExecuteDeleteAsync();

        _context.Groups.AddRange(snapshot.Groups);
        _context.Stubs.AddRange(snapshot.Stubs);
        await _context.SaveChangesAsync();
    }

    public async Task AppendRevisionAsync(int sequence, string summary, ConfigSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, PhoDbContext.JsonOptions);
        _context.ConfigRevisions.Add(new ConfigRevisionRecord
        {
            Sequence = sequence,
            Summary = summary,
            StateJson = json,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    public async Task TruncateAfterAsync(int sequence)
    {
        await _context.ConfigRevisions.Where(r => r.Sequence > sequence).ExecuteDeleteAsync();
    }

    public async Task<ConfigSnapshot?> GetRevisionSnapshotAsync(int sequence)
    {
        var record = await _context.ConfigRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Sequence == sequence);
        if (record is null) return null;

        return JsonSerializer.Deserialize<ConfigSnapshot>(record.StateJson, PhoDbContext.JsonOptions);
    }
}
