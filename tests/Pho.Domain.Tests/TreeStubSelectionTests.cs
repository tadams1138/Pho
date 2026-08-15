using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class TreeStubSelectionTests
{
    private static Stub StubIn(Guid? groupId, string name)
        => new()
        {
            Name = name,
            GroupId = groupId,
            Enabled = true,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + name) },
            Response = new ResponseDefinition()
        };

    [Fact]
    public void Nothing_selected_covers_no_stubs()
    {
        var stub = StubIn(null, "loose");
        var tree = GroupTree.Build(Array.Empty<Group>(), new[] { stub });

        TreeStubSelection.Collect(tree, Array.Empty<Guid>()).Should().BeEmpty();
    }

    [Fact]
    public void A_selected_stub_is_covered()
    {
        var stub = StubIn(null, "loose");
        var tree = GroupTree.Build(Array.Empty<Group>(), new[] { stub });

        TreeStubSelection.Collect(tree, new[] { stub.Id }).Should().Equal(stub);
    }

    [Fact]
    public void Selecting_a_group_covers_every_stub_nested_under_it()
    {
        var outer = new Group { Name = "outer" };
        var inner = new Group { Name = "inner", ParentGroupId = outer.Id };
        var direct = StubIn(outer.Id, "direct");
        var nested = StubIn(inner.Id, "nested");
        var loose = StubIn(null, "loose");
        var tree = GroupTree.Build(new[] { outer, inner }, new[] { direct, nested, loose });

        var covered = TreeStubSelection.Collect(tree, new[] { outer.Id });

        covered.Should().BeEquivalentTo(new[] { direct, nested });
    }

    [Fact]
    public void A_stub_selected_inside_a_selected_group_is_only_covered_once()
    {
        var group = new Group { Name = "g" };
        var stub = StubIn(group.Id, "s");
        var tree = GroupTree.Build(new[] { group }, new[] { stub });

        TreeStubSelection.Collect(tree, new[] { group.Id, stub.Id }).Should().Equal(stub);
    }

    [Fact]
    public void Selecting_an_empty_group_covers_nothing()
    {
        var group = new Group { Name = "g" };
        var loose = StubIn(null, "loose");
        var tree = GroupTree.Build(new[] { group }, new[] { loose });

        TreeStubSelection.Collect(tree, new[] { group.Id }).Should().BeEmpty();
    }
}
