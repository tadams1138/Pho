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
    private static HttpClient AdminClient()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
                services.AddSingleton<IStubRepository>(new FakeStubRepository()));
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
}
