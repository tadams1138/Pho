using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class TreeExpansionTests
{
    private static Stub StubIn(Guid? groupId, string name)
        => new()
        {
            Name = name,
            GroupId = groupId,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/" + name) },
            Response = new ResponseDefinition()
        };

    [Fact]
    public void All_group_ids_covers_every_depth()
    {
        var outer = new Group { Name = "outer" };
        var inner = new Group { Name = "inner", ParentGroupId = outer.Id };
        var tree = GroupTree.Build(new[] { outer, inner }, Array.Empty<Stub>());

        TreeExpansion.AllGroupIds(tree).Should().BeEquivalentTo(new[] { outer.Id, inner.Id });
    }

    [Fact]
    public void Stubs_are_not_part_of_the_expansion_scope()
    {
        var group = new Group { Name = "g" };
        var stub = StubIn(group.Id, "s");
        var tree = GroupTree.Build(new[] { group }, new[] { stub });

        TreeExpansion.AllGroupIds(tree).Should().Equal(group.Id);
    }

    [Fact]
    public void Nothing_selected_scopes_to_the_whole_tree()
    {
        var outer = new Group { Name = "outer" };
        var inner = new Group { Name = "inner", ParentGroupId = outer.Id };
        var tree = GroupTree.Build(new[] { outer, inner }, Array.Empty<Stub>());

        TreeExpansion.Scope(tree, Array.Empty<Guid>()).Should().BeEquivalentTo(new[] { outer.Id, inner.Id });
    }

    [Fact]
    public void A_selected_group_scopes_to_itself_and_its_descendants()
    {
        var outer = new Group { Name = "outer" };
        var inner = new Group { Name = "inner", ParentGroupId = outer.Id };
        var deeper = new Group { Name = "deeper", ParentGroupId = inner.Id };
        var sibling = new Group { Name = "sibling" };
        var tree = GroupTree.Build(new[] { outer, inner, deeper, sibling }, Array.Empty<Stub>());

        var scope = TreeExpansion.Scope(tree, new[] { outer.Id });

        scope.Should().BeEquivalentTo(new[] { outer.Id, inner.Id, deeper.Id });
    }

    [Fact]
    public void Selecting_only_stubs_scopes_to_no_groups_at_all()
    {
        var group = new Group { Name = "g" };
        var stub = StubIn(group.Id, "s");
        var tree = GroupTree.Build(new[] { group }, new[] { stub });

        TreeExpansion.Scope(tree, new[] { stub.Id }).Should().BeEmpty();
    }

    [Fact]
    public void A_group_selected_twice_over_appears_once_in_the_scope()
    {
        var outer = new Group { Name = "outer" };
        var inner = new Group { Name = "inner", ParentGroupId = outer.Id };
        var tree = GroupTree.Build(new[] { outer, inner }, Array.Empty<Stub>());

        TreeExpansion.Scope(tree, new[] { outer.Id, inner.Id }).Should().Equal(outer.Id, inner.Id);
    }
}
