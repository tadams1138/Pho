using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class GroupTreeTests
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
    public void Ungrouped_stubs_sit_at_the_root()
    {
        var stub = StubIn(null, "loose");

        var root = GroupTree.Build(Array.Empty<Group>(), new[] { stub });

        root.Group.Should().BeNull();
        root.Stubs.Should().ContainSingle(s => s.Name == "loose");
        root.Children.Should().BeEmpty();
    }

    [Fact]
    public void Nests_groups_and_places_stubs_under_their_group()
    {
        var vendor = new Group { Name = "Vendor1" };
        var auth = new Group { Name = "Auth", ParentGroupId = vendor.Id };
        var stub = StubIn(auth.Id, "login");

        var root = GroupTree.Build(new[] { vendor, auth }, new[] { stub });

        var vendorNode = root.Children.Should().ContainSingle(n => n.Group!.Name == "Vendor1").Subject;
        var authNode = vendorNode.Children.Should().ContainSingle(n => n.Group!.Name == "Auth").Subject;
        authNode.Stubs.Should().ContainSingle(s => s.Name == "login");
    }

    [Fact]
    public void Children_and_stubs_are_ordered_by_name()
    {
        var g = new Group { Name = "G" };
        var stubs = new[] { StubIn(g.Id, "b"), StubIn(g.Id, "a") };

        var root = GroupTree.Build(new[] { g }, stubs);

        root.Children.Single().Stubs.Select(s => s.Name).Should().ContainInOrder("a", "b");
    }
}
