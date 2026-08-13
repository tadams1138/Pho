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

public class TreeEditServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PhoDbContext _context;
    private readonly GroupService _groups;
    private readonly StubService _stubs;
    private readonly TreeEditService _tree;

    public TreeEditServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new PhoDbContext(
            new DbContextOptionsBuilder<PhoDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        var stubRepo = new EfStubRepository(_context);
        _groups = new GroupService(new EfGroupRepository(_context), stubRepo);
        _stubs = new StubService(stubRepo);
        _tree = new TreeEditService(_groups, _stubs);
    }

    private Task<Stub> Stub(string name, Guid? groupId = null)
        => _stubs.CreateAsync(new Stub
        {
            Name = name,
            GroupId = groupId,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + name) },
            Response = new ResponseDefinition()
        });

    [Fact]
    public async Task Deletes_several_stubs_and_groups_in_one_action()
    {
        var doomed = await _groups.CreateAsync("doomed");
        var kept = await _groups.CreateAsync("kept");
        var a = await Stub("a");
        var b = await Stub("b");
        await Stub("keep-me", kept.Id);

        var plan = await _tree.DeleteAsync(new[] { doomed.Id, a.Id, b.Id });

        plan.Describe().Should().Be("1 group and 2 stubs");
        (await _groups.ListAsync()).Should().ContainSingle(g => g.Id == kept.Id);
        (await _stubs.ListAsync()).Should().ContainSingle(s => s.Name == "keep-me");
    }

    [Fact]
    public async Task Deleting_a_group_and_a_stub_inside_it_does_not_fail_on_the_second_delete()
    {
        var group = await _groups.CreateAsync("group");
        var nested = await Stub("nested", group.Id);

        var plan = await _tree.DeleteAsync(new[] { group.Id, nested.Id });

        plan.GroupCount.Should().Be(1);
        plan.StubCount.Should().Be(1);
        (await _stubs.ListAsync()).Should().BeEmpty();
        (await _groups.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Planning_a_deletion_changes_nothing()
    {
        var group = await _groups.CreateAsync("group");
        await Stub("nested", group.Id);

        var plan = await _tree.PlanDeletionAsync(new[] { group.Id });

        plan.Describe().Should().Be("1 group and 1 stub");
        (await _groups.ListAsync()).Should().HaveCount(1);
        (await _stubs.ListAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Moves_a_stub_into_a_group_and_back_to_the_root()
    {
        var group = await _groups.CreateAsync("group");
        var stub = await Stub("wanderer");

        (await _tree.MoveAsync(new[] { stub.Id }, group.Id)).Should().Be(1);
        (await _stubs.GetAsync(stub.Id))!.GroupId.Should().Be(group.Id);

        (await _tree.MoveAsync(new[] { stub.Id }, null)).Should().Be(1);
        (await _stubs.GetAsync(stub.Id))!.GroupId.Should().BeNull();
    }

    [Fact]
    public async Task Nests_a_group_under_another_group()
    {
        var parent = await _groups.CreateAsync("parent");
        var child = await _groups.CreateAsync("child");

        await _tree.MoveAsync(new[] { child.Id }, parent.Id);

        (await _groups.ListAsync()).Single(g => g.Id == child.Id).ParentGroupId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Refuses_to_nest_a_group_inside_its_own_descendant()
    {
        var parent = await _groups.CreateAsync("parent");
        var child = await _groups.CreateAsync("child", parent.Id);

        var moved = await _tree.MoveAsync(new[] { parent.Id }, child.Id);

        moved.Should().Be(0);
        (await _groups.ListAsync()).Single(g => g.Id == parent.Id).ParentGroupId.Should().BeNull();
    }

    [Fact]
    public async Task Moving_a_group_leaves_the_stubs_travelling_with_it_alone()
    {
        var source = await _groups.CreateAsync("source");
        var target = await _groups.CreateAsync("target");
        var nested = await Stub("nested", source.Id);

        var moved = await _tree.MoveAsync(new[] { source.Id, nested.Id }, target.Id);

        moved.Should().Be(1, "only the group itself needs moving");
        (await _groups.ListAsync()).Single(g => g.Id == source.Id).ParentGroupId.Should().Be(target.Id);
        (await _stubs.GetAsync(nested.Id))!.GroupId.Should().Be(source.Id);
    }

    [Fact]
    public async Task Dropping_rows_where_they_already_live_moves_nothing()
    {
        var group = await _groups.CreateAsync("group");
        var stub = await Stub("settled", group.Id);

        (await _tree.MoveAsync(new[] { stub.Id }, group.Id)).Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
