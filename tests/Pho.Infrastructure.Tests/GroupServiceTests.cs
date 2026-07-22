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

public class GroupServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;
    private readonly GroupService _groups;
    private readonly StubService _stubs;

    public GroupServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new PhoDbContext(
            new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        var stubRepo = new EfStubRepository(_context);
        _groups = new GroupService(new EfGroupRepository(_context), stubRepo);
        _stubs = new StubService(stubRepo);
    }

    private Task<Stub> StubInGroup(Guid groupId, string name)
        => _stubs.CreateAsync(new Stub
        {
            Name = name,
            GroupId = groupId,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + name) },
            Response = new ResponseDefinition()
        });

    [Fact]
    public async Task Creates_nested_groups()
    {
        var parent = await _groups.CreateAsync("Vendor1");
        await _groups.CreateAsync("Auth", parent.Id);

        var all = await _groups.ListAsync();

        all.Should().HaveCount(2);
        all.Should().Contain(g => g.Name == "Auth" && g.ParentGroupId == parent.Id);
    }

    [Fact]
    public async Task Deleting_a_group_cascades_to_descendant_groups_and_their_stubs()
    {
        var parent = await _groups.CreateAsync("parent");
        var child = await _groups.CreateAsync("child", parent.Id);
        await StubInGroup(child.Id, "nested-stub");

        await _groups.DeleteAsync(parent.Id);

        (await _groups.ListAsync()).Should().BeEmpty();
        (await _stubs.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_group_leaves_unrelated_groups_and_stubs_untouched()
    {
        var target = await _groups.CreateAsync("target");
        var other = await _groups.CreateAsync("other");
        await StubInGroup(other.Id, "keep-me");

        await _groups.DeleteAsync(target.Id);

        (await _groups.ListAsync()).Should().ContainSingle(g => g.Id == other.Id);
        (await _stubs.ListAsync()).Should().ContainSingle(s => s.Name == "keep-me");
    }

    [Fact]
    public async Task Cannot_move_a_group_into_its_own_descendant()
    {
        var parent = await _groups.CreateAsync("parent");
        var child = await _groups.CreateAsync("child", parent.Id);

        var act = () => _groups.MoveAsync(parent.Id, child.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cannot_move_a_group_into_itself()
    {
        var group = await _groups.CreateAsync("g");

        var act = () => _groups.MoveAsync(group.Id, group.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
