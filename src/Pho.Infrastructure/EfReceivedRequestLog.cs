using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;

namespace Pho.Infrastructure;

/// <summary>Configured retention window for received-request logs (default 1 day).</summary>
public sealed class ReceivedRequestRetention
{
    public TimeSpan Value { get; init; } = TimeSpan.FromDays(1);
}

/// <summary>
/// EF Core/SQLite-backed received-request log. Recording prunes entries older than the retention
/// window; queries return newest-first, filterable by method and URL-path substring, and paged.
/// </summary>
public sealed class EfReceivedRequestLog : IReceivedRequestLog
{
    private readonly PhoDbContext _context;
    private readonly ReceivedRequestRetention _retention;

    public EfReceivedRequestLog(PhoDbContext context, ReceivedRequestRetention retention)
    {
        _context = context;
        _retention = retention;
    }

    public async Task RecordAsync(ReceivedRequest request)
    {
        _context.ReceivedRequests.Add(request);
        await _context.SaveChangesAsync();

        var cutoff = DateTime.UtcNow - _retention.Value;
        await _context.ReceivedRequests.Where(r => r.ReceivedAt < cutoff).ExecuteDeleteAsync();
    }

    public async Task<PagedResult<ReceivedRequest>> QueryAsync(string? method, string? pathContains, int page, int pageSize)
    {
        var query = _context.ReceivedRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(r => r.Method == method);

        if (!string.IsNullOrWhiteSpace(pathContains))
            query = query.Where(r => r.Path.Contains(pathContains));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.ReceivedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ReceivedRequest>(items, total, page, pageSize);
    }

    public async Task ClearAsync()
    {
        await _context.ReceivedRequests.ExecuteDeleteAsync();
    }
}
