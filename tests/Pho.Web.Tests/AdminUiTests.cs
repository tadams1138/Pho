using System.Net;
using System.Net.Http;
using Regex = System.Text.RegularExpressions.Regex;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pho.Domain;
using Xunit;

namespace Pho.Web.Tests;

public class AdminUiTests
{
    // Default traffic policy: under TestServer the local port is 0 (not the mock port),
    // so requests are treated as admin traffic and served by the Blazor UI.
    private static HttpClient AdminClient(
        IEnumerable<Group>? groups = null,
        IEnumerable<Stub>? stubs = null,
        string? pathBase = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            if (pathBase is not null)
            {
                builder.UseSetting("Pho:PathBase", pathBase);
            }
            // Static web assets only auto-load in Development; load them here so
            // UseStaticFiles can serve _framework/blazor.web.js under Testing.
            builder.UseStaticWebAssets();
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStubRepository>(new FakeStubRepository(stubs));
                services.AddSingleton<IGroupRepository>(new FakeGroupRepository(groups));
                services.AddSingleton<IConfigHistoryStore>(new FakeConfigHistoryStore());
                services.AddSingleton<IConfigPorter>(new FakeConfigPorter());
            });
        });
        return factory.CreateClient();
    }

    [Fact]
    public async Task Root_serves_the_stubs_page()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Stubs");
    }

    [Fact]
    public async Task Every_link_the_page_emits_is_relative_so_it_survives_a_sub_path_proxy()
    {
        var client = AdminClient();

        var html = await client.GetStringAsync("/");

        // An absolute link resolves to the proxy's root, dropping the prefix it was reached through.
        // The <base> element is the one absolute href on the page — it is what the rest resolve against.
        var links = Regex.Replace(html, "<base [^>]*>", "");
        links.Should().NotContain("href=\"/", "app links must resolve against the <base href>, not the host root");
        links.Should().NotContain("src=\"/", "the Blazor script must load relative to the <base href> too");
        html.Should().Contain("src=\"_framework/blazor.web.js\"");
    }

    [Fact]
    public async Task Served_at_a_host_root_the_base_href_is_just_a_slash()
    {
        var client = AdminClient();

        var html = await client.GetStringAsync("/");

        html.Should().Contain("<base href=\"/\"");
    }

    [Fact]
    public async Task A_proxy_announcing_the_prefix_it_stripped_moves_the_base_href_under_it()
    {
        var client = AdminClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Prefix", "/Pho");

        var html = await client.GetStringAsync("/");

        html.Should().Contain("<base href=\"/Pho/\"", "relative links then resolve under the proxy's sub-path");
    }

    [Theory]
    [InlineData("/mocks")]
    [InlineData("mocks")]
    [InlineData("/mocks/")]
    public async Task The_prefix_can_be_configured_for_a_proxy_that_strips_it_silently(string configured)
    {
        var client = AdminClient(pathBase: configured);

        var html = await client.GetStringAsync("/");

        html.Should().Contain("<base href=\"/mocks/\"", "the prefix is accepted however it is written");
    }

    [Fact]
    public async Task Blazor_script_is_served_with_a_real_body()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/_framework/blazor.web.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsByteArrayAsync();
        body.Length.Should().BeGreaterThan(1000, "blazor.web.js must be delivered with its real content, not an empty 200");
    }

    [Fact]
    public async Task Backup_controls_sit_above_the_authoring_controls_and_the_tree()
    {
        var client = AdminClient(new[] { new Group { Name = "Vendor1" } });

        var html = await client.GetStringAsync("/");

        var export = html.IndexOf("\"export\"", StringComparison.Ordinal);
        var newStub = html.IndexOf("New stub", StringComparison.Ordinal);
        var tree = html.IndexOf("class=\"tree\"", StringComparison.Ordinal);

        export.Should().BeGreaterThan(0);
        export.Should().BeLessThan(newStub, "export/import belongs at the top of the page");
        newStub.Should().BeLessThan(tree, "the tree comes after the controls that add to it");
    }

    [Fact]
    public async Task Tree_rows_are_draggable_and_carry_no_delete_button_of_their_own()
    {
        var group = new Group { Name = "Vendor1" };
        var stub = new Stub
        {
            Name = "login",
            GroupId = group.Id,
            Request = new RequestMatcher { Path = new PathMatcher(PathMatchType.Exact, "/login") },
            Response = new ResponseDefinition()
        };
        var client = AdminClient(new[] { group }, new[] { stub });

        var html = await client.GetStringAsync("/");

        html.Should().Contain("Vendor1");
        html.Should().Contain("draggable=\"true\"", "rows are rearranged by dragging");
        html.Should().Contain("Delete selected", "deleting is a toolbar action over the selection");
        html.Should().NotContain(">Delete group<").And.NotContain(">Delete<");
    }

    [Fact]
    public async Task Groups_start_collapsed_and_can_be_expanded_or_collapsed_in_bulk()
    {
        var group = new Group { Name = "Vendor1" };
        var client = AdminClient(new[] { group }, new[] { StubIn(group.Id, "login", "/login") });

        var html = await client.GetStringAsync("/");

        html.Should().Contain("Vendor1");
        html.Should().NotContain("login", "the app opens with every group collapsed");
        html.Should().Contain("Expand all").And.Contain("Collapse all");
    }

    [Fact]
    public async Task Enabling_and_disabling_are_toolbar_actions_over_the_selection()
    {
        var client = AdminClient(stubs: new[] { StubIn(null, "login", "/login") });

        var html = await client.GetStringAsync("/");

        html.Should().Contain("Delete selected");
        html.Should().MatchRegex(@">\s*Enable").And.MatchRegex(@">\s*Disable");
        html.Should().NotContain("enabled-toggle", "a stub's enabled state is not toggled row by row");
    }

    [Fact]
    public async Task An_unnamed_stub_shows_its_method_and_path_in_the_tree()
    {
        var client = AdminClient(stubs: new[] { StubIn(null, name: "", path: "/sessions") });

        var html = await client.GetStringAsync("/");

        html.Should().Contain("GET /sessions");
    }

    [Fact]
    public async Task A_named_stub_shows_only_its_name_in_the_tree()
    {
        var client = AdminClient(stubs: new[] { StubIn(null, "login", "/sessions") });

        var html = await client.GetStringAsync("/");

        html.Should().Contain("login");
        html.Should().NotContain("GET /sessions", "a named row does not repeat what it matches");
    }

    private static Stub StubIn(Guid? groupId, string name, string path)
        => new()
        {
            Name = name,
            GroupId = groupId,
            Request = new RequestMatcher { Method = HttpMethodMatch.Get, Path = new PathMatcher(PathMatchType.Exact, path) },
            Response = new ResponseDefinition()
        };

    [Fact]
    public async Task The_stub_editor_lives_beside_the_tree_rather_than_on_its_own_page()
    {
        var client = AdminClient();

        var html = await client.GetStringAsync("/");

        html.Should().Contain("editor-pane");
        html.Should().Contain("Select a stub to edit it");
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = AdminClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
