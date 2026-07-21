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

public class StubServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;
    private readonly StubService _service;

    public StubServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new PhoDbContext(
            new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _service = new StubService(new EfStubRepository(_context));
    }

    private static Stub NewStub(string name = "s", bool enabled = true)
        => new()
        {
            Name = name,
            Enabled = enabled,
            Request = new RequestMatcher
            {
                Method = HttpMethodMatch.Get,
                Path = new PathMatcher(PathMatchType.Exact, "/a")
            },
            Response = new ResponseDefinition { Status = 200, Body = "hi" }
        };

    [Fact]
    public async Task Creates_and_lists_a_stub()
    {
        await _service.CreateAsync(NewStub("first"));

        var all = await _service.ListAsync();

        all.Should().ContainSingle(s => s.Name == "first");
    }

    [Fact]
    public async Task Deletes_a_stub()
    {
        var created = await _service.CreateAsync(NewStub());

        await _service.DeleteAsync(created.Id);

        (await _service.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_creates_a_disabled_copy_with_a_copy_name_and_new_id()
    {
        var original = await _service.CreateAsync(NewStub("Vendor call", enabled: true));

        var copy = await _service.DuplicateAsync(original.Id);

        copy.Id.Should().NotBe(original.Id);
        copy.Name.Should().Be("Copy of Vendor call");
        copy.Enabled.Should().BeFalse();
        copy.Request.Should().Be(original.Request);
        copy.Response.Should().Be(original.Response);
        (await _service.ListAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task SetEnabled_toggles_the_stub()
    {
        var created = await _service.CreateAsync(NewStub(enabled: true));

        await _service.SetEnabledAsync(created.Id, false);

        var reloaded = await _service.GetAsync(created.Id);
        reloaded!.Enabled.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
