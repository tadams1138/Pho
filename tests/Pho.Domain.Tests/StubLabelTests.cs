using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class StubLabelTests
{
    private static Stub StubNamed(string name, HttpMethodMatch method = HttpMethodMatch.Get, string path = "/users/1")
        => new()
        {
            Name = name,
            Request = new RequestMatcher { Method = method, Path = new PathMatcher(PathMatchType.Exact, path) },
            Response = new ResponseDefinition()
        };

    [Fact]
    public void Uses_the_name_when_there_is_one()
    {
        var stub = StubNamed("login");

        StubLabel.For(stub).Should().Be("login");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Falls_back_to_method_and_path_when_the_name_is_empty(string name)
    {
        var stub = StubNamed(name, HttpMethodMatch.Post, "/sessions");

        StubLabel.For(stub).Should().Be("POST /sessions");
    }

    [Fact]
    public void Method_is_upper_cased_for_the_fallback()
    {
        StubLabel.ForRequest(HttpMethodMatch.Patch, "/things/7").Should().Be("PATCH /things/7");
        StubLabel.ForRequest(HttpMethodMatch.Any, "/things/*").Should().Be("ANY /things/*");
    }

    [Fact]
    public void A_blank_path_leaves_no_trailing_space()
    {
        StubLabel.ForRequest(HttpMethodMatch.Get, "  ").Should().Be("GET");
    }

    [Fact]
    public void Reports_whether_a_stub_carries_a_name_of_its_own()
    {
        StubLabel.HasName(StubNamed("login")).Should().BeTrue();
        StubLabel.HasName(StubNamed("")).Should().BeFalse();
        StubLabel.HasName(StubNamed(" ")).Should().BeFalse();
    }

    [Fact]
    public void Unnamed_stubs_sort_by_the_label_the_tree_shows()
    {
        var groups = new List<Group>();
        var stubs = new List<Stub>
        {
            StubNamed("", HttpMethodMatch.Post, "/zeta"),
            StubNamed("", HttpMethodMatch.Get, "/alpha")
        };

        var rows = TreeFlattener.Flatten(GroupTree.Build(groups, stubs));

        rows.Select(r => r.Label).Should().Equal("GET /alpha", "POST /zeta");
    }
}
