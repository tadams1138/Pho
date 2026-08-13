using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class TreeFlattenerTests
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
    public void Flattens_nested_groups_in_display_order_with_depth()
    {
        var vendor = new Group { Name = "Vendor1" };
        var auth = new Group { Name = "Auth", ParentGroupId = vendor.Id };
        var stubs = new[] { StubIn(auth.Id, "login"), StubIn(null, "loose") };

        var rows = TreeFlattener.Flatten(GroupTree.Build(new[] { vendor, auth }, stubs));

        rows.Select(r => r.Name).Should().ContainInOrder("Vendor1", "Auth", "login", "loose");
        rows.Select(r => r.Depth).Should().ContainInOrder(0, 1, 2, 0);
    }

    [Fact]
    public void A_collapsed_group_hides_its_descendants()
    {
        var vendor = new Group { Name = "Vendor1" };
        var auth = new Group { Name = "Auth", ParentGroupId = vendor.Id };
        var stubs = new[] { StubIn(auth.Id, "login") };
        var tree = GroupTree.Build(new[] { vendor, auth }, stubs);

        var rows = TreeFlattener.Flatten(tree, new HashSet<Guid> { vendor.Id });

        rows.Select(r => r.Name).Should().Equal("Vendor1");
        rows.Single().Expanded.Should().BeFalse();
        rows.Single().HasChildren.Should().BeTrue();
    }

    [Fact]
    public void Rows_carry_their_parent_group_for_navigation_and_drops()
    {
        var vendor = new Group { Name = "Vendor1" };
        var stub = StubIn(vendor.Id, "login");

        var rows = TreeFlattener.Flatten(GroupTree.Build(new[] { vendor }, new[] { stub }));

        var groupRow = rows.Single(r => r.Kind == TreeRowKind.Group);
        var stubRow = rows.Single(r => r.Kind == TreeRowKind.Stub);
        groupRow.ParentGroupId.Should().BeNull();
        stubRow.ParentGroupId.Should().Be(vendor.Id);
        stubRow.DropTargetGroupId.Should().Be(vendor.Id, "dropping onto a stub means 'into the group that holds it'");
    }

    [Fact]
    public void An_empty_group_reports_no_children()
    {
        var empty = new Group { Name = "Empty" };

        var rows = TreeFlattener.Flatten(GroupTree.Build(new[] { empty }, Array.Empty<Stub>()));

        rows.Single().HasChildren.Should().BeFalse();
    }
}
