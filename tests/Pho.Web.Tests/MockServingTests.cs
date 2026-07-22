using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pho.Domain;
using Pho.Web;
using Xunit;

namespace Pho.Web.Tests;

public class MockServingTests
{
    private static HttpClient ClientWith(params Stub[] stubs)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStubStore>(new InMemoryStubStore(stubs));
                services.AddSingleton<IMockTrafficPolicy, AlwaysMockTrafficPolicy>();
            });
        });
        return factory.CreateClient();
    }

    private static Stub GetStub(string path, int status, string body, bool enabled = true, string name = "s")
        => new()
        {
            Name = name,
            Enabled = enabled,
            Request = new RequestMatcher
            {
                Method = HttpMethodMatch.Get,
                Path = new PathMatcher(PathMatchType.Exact, path)
            },
            Response = new ResponseDefinition { Status = status, Body = body }
        };

    [Fact]
    public async Task Unmatched_request_returns_404()
    {
        var client = ClientWith();

        var response = await client.GetAsync("/nothing-here");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Matched_request_returns_the_configured_response()
    {
        var client = ClientWith(GetStub("/hello", 200, "world"));

        var response = await client.GetAsync("/hello");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("world");
    }

    [Fact]
    public async Task Ambiguous_match_returns_500()
    {
        var client = ClientWith(
            GetStub("/dup", 200, "a", name: "alpha"),
            GetStub("/dup", 200, "b", name: "beta"));

        var response = await client.GetAsync("/dup");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
