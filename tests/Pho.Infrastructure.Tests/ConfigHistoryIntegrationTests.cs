using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pho.Domain;
using Pho.Infrastructure;
using Xunit;

namespace Pho.Infrastructure.Tests;

public class ConfigHistoryIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;
    private readonly StubService _stubs;
    private readonly ConfigHistoryService _history;

    public ConfigHistoryIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new PhoDbContext(
            new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _stubs = new StubService(new EfStubRepository(_context));
        _history = new ConfigHistoryService(new EfConfigHistoryStore(_context));
    }

    private Task<Stub> AddStub(string name)
        => _stubs.CreateAsync(new Stub
        {
            Name = name,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + name) },
            Response = new ResponseDefinition { Status = 200, Body = name }
        });

    [Fact]
    public async Task Undo_and_redo_restore_persisted_configuration()
    {
        await AddStub("A");
        await _history.RecordAsync("add A");
        await AddStub("B");
        await _history.RecordAsync("add B");

        (await _stubs.ListAsync()).Should().HaveCount(2);

        await _history.UndoAsync();
        (await _stubs.ListAsync()).Select(s => s.Name).Should().BeEquivalentTo("A");

        await _history.UndoAsync();
        (await _stubs.ListAsync()).Should().BeEmpty();

        await _history.RedoAsync();
        (await _stubs.ListAsync()).Select(s => s.Name).Should().BeEquivalentTo("A");
    }

    [Fact]
    public async Task Restored_stub_keeps_its_definition()
    {
        await AddStub("hello");
        await _history.RecordAsync("add hello");
        await AddStub("second");
        await _history.RecordAsync("add second");

        await _history.UndoAsync();

        var restored = (await _stubs.ListAsync()).Single();
        restored.Name.Should().Be("hello");
        restored.Request.Path.Value.Should().Be("/hello");
        restored.Response.Body.Should().Be("hello");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
