using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class TreeDeletionTests
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
    public void Nothing_selected_plans_nothing()
    {
        var tree = GroupTree.Build(Array.Empty<Group>(), Array.Empty<Stub>());

        var plan = TreeDeletion.Plan(tree, Array.Empty<Guid>());

        plan.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Selecting_a_group_and_a_stub_deletes_both()
    {
        var group = new Group { Name = "g" };
        var loose = StubIn(null, "loose");
        var tree = GroupTree.Build(new[] { group }, new[] { loose });

        var plan = TreeDeletion.Plan(tree, new[] { group.Id, loose.Id });

        plan.GroupIds.Should().Equal(group.Id);
        plan.StubIds.Should().Equal(loose.Id);
        plan.GroupCount.Should().Be(1);
        plan.StubCount.Should().Be(1);
    }

    [Fact]
    public void A_selected_group_counts_its_whole_subtree_but_deletes_only_the_top_group()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };
        var nested = StubIn(child.Id, "nested");
        var tree = GroupTree.Build(new[] { parent, child }, new[] { nested });

        var plan = TreeDeletion.Plan(tree, new[] { parent.Id });

        plan.GroupIds.Should().Equal(parent.Id, "the cascade removes the descendants");
        plan.StubIds.Should().BeEmpty();
        plan.GroupCount.Should().Be(2);
        plan.StubCount.Should().Be(1);
    }

    [Fact]
    public void Rows_already_covered_by_a_selected_ancestor_are_not_deleted_twice()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };
        var nested = StubIn(child.Id, "nested");
        var tree = GroupTree.Build(new[] { parent, child }, new[] { nested });

        var plan = TreeDeletion.Plan(tree, new[] { parent.Id, child.Id, nested.Id });

        plan.GroupIds.Should().Equal(parent.Id);
        plan.StubIds.Should().BeEmpty();
        plan.GroupCount.Should().Be(2);
        plan.StubCount.Should().Be(1);
    }

    [Fact]
    public void Describes_what_will_be_removed_for_the_confirmation_prompt()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };
        var tree = GroupTree.Build(new[] { parent, child }, new[] { StubIn(child.Id, "a"), StubIn(null, "b") });

        TreeDeletion.Plan(tree, new[] { parent.Id }).Describe().Should().Be("2 groups and 1 stub");
        TreeDeletion.Plan(tree, tree.Stubs.Select(s => s.Id).ToArray()).Describe().Should().Be("1 stub");
    }
}
