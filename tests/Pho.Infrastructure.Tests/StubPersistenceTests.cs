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

public class StubPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;

    public StubPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = NewContext();
        _context.Database.EnsureCreated();
    }

    private PhoDbContext NewContext()
        => new(new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Saves_and_reloads_a_stub_with_its_matcher_and_response()
    {
        var stub = new Stub
        {
            Name = "hello",
            Request = new RequestMatcher
            {
                Method = HttpMethodMatch.Get,
                Path = new PathMatcher(PathMatchType.Exact, "/hello"),
                Headers = new List<ParamMatcher> { new("X-Test", new MatchRule(MatchRuleType.Present)) }
            },
            Response = new ResponseDefinition
            {
                Status = 200,
                Body = "world",
                Headers = new List<HeaderValue> { new("Content-Type", "text/plain") }
            }
        };
        _context.Stubs.Add(stub);
        await _context.SaveChangesAsync();

        await using var verify = NewContext();
        var loaded = verify.Stubs.Single();

        loaded.Name.Should().Be("hello");
        loaded.Request.Method.Should().Be(HttpMethodMatch.Get);
        loaded.Request.Path.Value.Should().Be("/hello");
        loaded.Request.Headers.Should().ContainSingle(h => h.Name == "X-Test");
        loaded.Response.Status.Should().Be(200);
        loaded.Response.Body.Should().Be("world");
    }

    [Fact]
    public async Task Reloaded_stub_still_matches_correctly()
    {
        _context.Stubs.Add(new Stub
        {
            Name = "hdr",
            Request = new RequestMatcher
            {
                Method = HttpMethodMatch.Get,
                Path = new PathMatcher(PathMatchType.Exact, "/hello"),
                Headers = new List<ParamMatcher> { new("X-Test", new MatchRule(MatchRuleType.Present)) }
            },
            Response = new ResponseDefinition { Status = 200 }
        });
        await _context.SaveChangesAsync();

        await using var verify = NewContext();
        var loaded = verify.Stubs.Single();

        var withHeader = new HttpRequestData("GET", "/hello",
            Headers: new Dictionary<string, string?> { ["X-Test"] = "1" });
        var withoutHeader = new HttpRequestData("GET", "/hello");

        MockResolver.Resolve(new[] { loaded }, withHeader).Outcome.Should().Be(MatchOutcome.MatchedOne);
        MockResolver.Resolve(new[] { loaded }, withoutHeader).Outcome.Should().Be(MatchOutcome.NoMatch);
    }

    [Fact]
    public async Task EfStubStore_reads_persisted_stubs()
    {
        _context.Stubs.Add(new Stub
        {
            Name = "a",
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/a") },
            Response = new ResponseDefinition()
        });
        await _context.SaveChangesAsync();

        var store = new EfStubStore(NewContext());

        store.GetAll().Should().HaveCount(1);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
