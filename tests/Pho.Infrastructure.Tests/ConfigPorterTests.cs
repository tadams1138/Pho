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

public class ConfigPorterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;
    private readonly EfConfigPorter _porter;
    private readonly StubService _stubs;
    private readonly GroupService _groups;

    public ConfigPorterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new PhoDbContext(
            new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        var stubRepo = new EfStubRepository(_context);
        _porter = new EfConfigPorter(_context);
        _stubs = new StubService(stubRepo);
        _groups = new GroupService(new EfGroupRepository(_context), stubRepo);
    }

    private Task<Stub> AddStub(string name, Guid? groupId = null)
        => _stubs.CreateAsync(new Stub
        {
            Name = name,
            GroupId = groupId,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + name) },
            Response = new ResponseDefinition { Status = 200, Body = name }
        });

    [Fact]
    public async Task Round_trips_the_full_configuration_via_replace_all()
    {
        var group = await _groups.CreateAsync("Vendor1");
        await AddStub("login", group.Id);
        await AddStub("loose");
        var exported = await _porter.ExportJsonAsync();

        // Wipe, then re-import.
        await _porter.ImportJsonAsync("""{"Stubs":[],"Groups":[]}""", ImportMode.ReplaceAll);
        (await _stubs.ListAsync()).Should().BeEmpty();

        await _porter.ImportJsonAsync(exported, ImportMode.ReplaceAll);

        var stubs = await _stubs.ListAsync();
        var groups = await _groups.ListAsync();
        stubs.Select(s => s.Name).Should().BeEquivalentTo("login", "loose");
        groups.Should().ContainSingle(g => g.Name == "Vendor1");
        stubs.Single(s => s.Name == "login").GroupId.Should().Be(group.Id);
    }

    [Fact]
    public async Task Replace_all_removes_existing_config_not_in_the_file()
    {
        await AddStub("old");
        var emptyExport = await _porter.ExportJsonAsync(); // has "old"
        await AddStub("newer");

        // Import the earlier export (only "old") with replace-all.
        await _porter.ImportJsonAsync(emptyExport, ImportMode.ReplaceAll);

        (await _stubs.ListAsync()).Select(s => s.Name).Should().BeEquivalentTo("old");
    }

    [Fact]
    public async Task Merge_updates_existing_and_adds_new()
    {
        var stub = await AddStub("keep");
        var export = await _porter.ExportJsonAsync();       // snapshot with "keep"
        // Rename the live stub, then merge the old export back: name reverts, live-only stays.
        stub.Name = "renamed";
        await _stubs.UpdateAsync(stub);
        await AddStub("extra");

        await _porter.ImportJsonAsync(export, ImportMode.Merge);

        var names = (await _stubs.ListAsync()).Select(s => s.Name).ToList();
        names.Should().Contain("keep");    // merged back to exported name
        names.Should().Contain("extra");   // not in file, left untouched
    }

    [Fact]
    public async Task Invalid_json_is_rejected_and_leaves_config_unchanged()
    {
        await AddStub("safe");

        var act = () => _porter.ImportJsonAsync("not json at all", ImportMode.ReplaceAll);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await _stubs.ListAsync()).Should().ContainSingle(s => s.Name == "safe");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
