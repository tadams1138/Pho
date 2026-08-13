using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class TreeMoveRulesTests
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
    public void A_group_can_be_dropped_into_another_group()
    {
        var a = new Group { Name = "a" };
        var b = new Group { Name = "b" };

        TreeMoveRules.CanMoveGroup(new[] { a, b }, a.Id, b.Id).Should().BeTrue();
    }

    [Fact]
    public void A_group_cannot_be_dropped_into_itself_or_its_own_descendant()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };
        var grandchild = new Group { Name = "grandchild", ParentGroupId = child.Id };
        var groups = new[] { parent, child, grandchild };

        TreeMoveRules.CanMoveGroup(groups, parent.Id, parent.Id).Should().BeFalse();
        TreeMoveRules.CanMoveGroup(groups, parent.Id, child.Id).Should().BeFalse();
        TreeMoveRules.CanMoveGroup(groups, parent.Id, grandchild.Id).Should().BeFalse();
    }

    [Fact]
    public void Dropping_a_row_where_it_already_lives_is_not_a_move()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };

        TreeMoveRules.CanMoveGroup(new[] { parent, child }, child.Id, parent.Id).Should().BeFalse();
        TreeMoveRules.CanMoveStub(StubIn(parent.Id, "s"), parent.Id).Should().BeFalse();
        TreeMoveRules.CanMoveStub(StubIn(parent.Id, "s"), null).Should().BeTrue();
    }

    [Fact]
    public void Moving_a_selection_moves_only_its_topmost_groups()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };
        var target = new Group { Name = "target" };

        var top = TreeMoveRules.TopMostGroups(new[] { parent, child, target }, new[] { parent.Id, child.Id });

        top.Should().Equal(parent.Id, "the child rides along with its parent");
    }

    [Fact]
    public void A_stub_inside_a_moving_group_is_left_alone()
    {
        var parent = new Group { Name = "parent" };
        var child = new Group { Name = "child", ParentGroupId = parent.Id };
        var nested = StubIn(child.Id, "nested");
        var loose = StubIn(null, "loose");

        var stubs = TreeMoveRules.StubsToMove(
            new[] { parent, child },
            new[] { nested, loose },
            movingGroupIds: new[] { parent.Id },
            selectedIds: new[] { nested.Id, loose.Id });

        stubs.Should().Equal(loose.Id);
    }
}
