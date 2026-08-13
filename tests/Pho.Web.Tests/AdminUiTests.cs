using System.Net;
using System.Net.Http;
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
    private static HttpClient AdminClient(IEnumerable<Group>? groups = null, IEnumerable<Stub>? stubs = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
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

        var export = html.IndexOf("/export", StringComparison.Ordinal);
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

        html.Should().Contain("Vendor1").And.Contain("login");
        html.Should().Contain("draggable=\"true\"", "rows are rearranged by dragging");
        html.Should().Contain("Delete selected", "deleting is a toolbar action over the selection");
        html.Should().NotContain(">Delete group<").And.NotContain(">Delete<");
    }

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
