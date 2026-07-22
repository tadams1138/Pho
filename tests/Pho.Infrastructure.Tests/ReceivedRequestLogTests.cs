using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;
using Pho.Infrastructure;
using Xunit;

namespace Pho.Infrastructure.Tests;

public class ReceivedRequestLogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;
    private readonly EfReceivedRequestLog _log;

    public ReceivedRequestLogTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new PhoDbContext(
            new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _log = new EfReceivedRequestLog(_context, new ReceivedRequestRetention { Value = TimeSpan.FromDays(1) });
    }

    private static ReceivedRequest Request(string method, string path, DateTime at, MatchOutcome outcome = MatchOutcome.NoMatch)
        => new()
        {
            ReceivedAt = at,
            Method = method,
            Path = path,
            Query = "?x=1",
            Headers = new Dictionary<string, string?> { ["Host"] = "localhost" },
            Body = "body",
            Outcome = outcome,
            ResponseStatus = 404
        };

    [Fact]
    public async Task Records_and_returns_newest_first()
    {
        var now = DateTime.UtcNow;
        await _log.RecordAsync(Request("GET", "/a", now.AddSeconds(-2)));
        await _log.RecordAsync(Request("GET", "/b", now.AddSeconds(-1)));
        await _log.RecordAsync(Request("GET", "/c", now));

        var result = await _log.QueryAsync(null, null, 1, 10);

        result.Items.Select(r => r.Path).Should().ContainInOrder("/c", "/b", "/a");
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Filters_by_method_and_path_substring()
    {
        var now = DateTime.UtcNow;
        await _log.RecordAsync(Request("GET", "/users/1", now));
        await _log.RecordAsync(Request("POST", "/users/1", now));
        await _log.RecordAsync(Request("GET", "/orders", now));

        (await _log.QueryAsync("GET", null, 1, 10)).TotalCount.Should().Be(2);
        (await _log.QueryAsync(null, "users", 1, 10)).TotalCount.Should().Be(2);
        (await _log.QueryAsync("GET", "users", 1, 10)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Pages_results()
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
            await _log.RecordAsync(Request("GET", $"/p{i}", now.AddSeconds(i)));

        var page1 = await _log.QueryAsync(null, null, 1, 2);
        var page2 = await _log.QueryAsync(null, null, 2, 2);

        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.Items.Should().NotIntersectWith(page2.Items);
    }

    [Fact]
    public async Task Reloaded_request_keeps_headers_and_outcome()
    {
        await _log.RecordAsync(Request("GET", "/x", DateTime.UtcNow, MatchOutcome.MatchedOne));

        var reloaded = (await _log.QueryAsync(null, null, 1, 10)).Items.Single();

        reloaded.Headers.Should().ContainKey("Host");
        reloaded.Outcome.Should().Be(MatchOutcome.MatchedOne);
        reloaded.Query.Should().Be("?x=1");
    }

    [Fact]
    public async Task Prunes_entries_older_than_retention_on_record()
    {
        // An old entry inserted directly (2 days ago) is pruned when a fresh one is recorded.
        _context.ReceivedRequests.Add(Request("GET", "/old", DateTime.UtcNow.AddDays(-2)));
        await _context.SaveChangesAsync();

        await _log.RecordAsync(Request("GET", "/fresh", DateTime.UtcNow));

        var result = await _log.QueryAsync(null, null, 1, 10);
        result.Items.Should().ContainSingle(r => r.Path == "/fresh");
    }

    [Fact]
    public async Task Clear_empties_the_log()
    {
        await _log.RecordAsync(Request("GET", "/a", DateTime.UtcNow));

        await _log.ClearAsync();

        (await _log.QueryAsync(null, null, 1, 10)).TotalCount.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
