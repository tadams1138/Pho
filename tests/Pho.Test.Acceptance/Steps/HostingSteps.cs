using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>How Pho is hosted: health, its two surfaces, reverse proxies, persistence and retention.</summary>
[Binding]
public sealed class HostingSteps(PhoApp app, MockSteps mock)
{
    private HttpResponseMessage? _response;
    private string _html = "";

    [Given("Pho is running")]
    public void Running() => _ = app.Factory;

    [When("an orchestrator asks the admin surface whether Pho is healthy")]
    public async Task AskHealth() => _response = await app.Admin.GetAsync("health");

    [Then("Pho reports that it is healthy")]
    public async Task ReportsHealthy()
    {
        _response!.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    // ---- the real thing, on real sockets --------------------------------------------------------

    private WebApplicationFactory<Program>? _kestrel;
    private readonly HttpClient _client = new();

    [Given("Pho has been started with its default settings")]
    public void StartedWithDefaults()
    {
        var database = Path.Combine(Path.GetTempPath(), $"pho-acceptance-{Guid.NewGuid():N}.db");
        _kestrel = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Pho", $"Data Source={database}");
        });
        _kestrel.UseKestrel();
        _kestrel.StartServer();
        app.OnDispose(() =>
        {
            _kestrel.Dispose();
            _client.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(database);
        });
    }

    [Then("the admin UI is served at its default address")]
    public async Task AdminAtDefault()
    {
        var html = await _client.GetStringAsync($"http://localhost:{PhoApp.DefaultAdminPort}/");
        html.Should().Contain("<title>Pho");
    }

    [Then("the mock surface answers at its own default address")]
    public async Task MockAtDefault()
    {
        var response = await _client.GetAsync($"http://localhost:{PhoApp.DefaultMockPort}/anything");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("No stub matched");
    }

    [Then("the service reports that it is healthy at the admin address")]
    public async Task HealthyAtDefault()
        => (await _client.GetStringAsync($"http://localhost:{PhoApp.DefaultAdminPort}/health")).Should().Be("Healthy");

    // ---- the two surfaces ---------------------------------------------------------------------

    /// <summary>The pages and endpoints the admin surface serves.</summary>
    private static readonly string[] AdminPaths = ["/", "/received", "/health", "/export"];

    [Given("a stub for every path the admin surface serves")]
    public async Task StubPerAdminPath()
    {
        foreach (var path in AdminPaths)
        {
            await app.Get<StubService>().CreateAsync(new Stub
            {
                Name = $"Mock of {path}",
                Request = new RequestMatcher { Method = HttpMethodMatch.Get, Path = new PathMatcher(PathMatchType.Exact, path) },
                Response = new ResponseDefinition { Body = $"mocked {path}" }
            });
        }
    }

    [Given("no stubs exist")]
    public async Task NoStubs() => (await app.Get<StubService>().ListAsync()).Should().BeEmpty();

    [Then("each of those paths is answered by its stub on the mock surface")]
    public async Task EachAnsweredByStub()
    {
        foreach (var path in AdminPaths)
        {
            await mock.Send("GET", path);
            await mock.ThenAnsweredBy($"Mock of {path}");
        }
    }

    [Then("none of the paths the admin surface serves is answered on the mock surface")]
    public async Task NoneAnswered()
    {
        foreach (var path in AdminPaths)
        {
            await mock.Send("GET", path);
            mock.Last.Body.Should().Be("No stub matched this request.", "GET {0} on the mock surface", path);
        }
    }

    [Then("the admin surface still serves its own pages at those paths")]
    public async Task AdminStillServes()
    {
        (await app.Admin.GetStringAsync("/")).Should().Contain("<title>Pho");
        (await app.Admin.GetStringAsync("received")).Should().Contain("Received requests");
        (await app.Admin.GetStringAsync("health")).Should().Be("Healthy");
        (await app.Admin.GetAsync("export")).Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    private const int ConfiguredAdminPort = 9100;
    private const int ConfiguredMockPort = 9101;

    [Given("Pho is configured with its own addresses for the admin UI and for mock traffic")]
    public void ConfigurePorts()
    {
        app.Settings["Pho:AdminPort"] = ConfiguredAdminPort.ToString();
        app.Settings["Pho:MockPort"] = ConfiguredMockPort.ToString();
    }

    [Then("""^(\S+) (\S+) is answered by "([^"]*)" at the configured mock address$""")]
    public async Task MockAtConfigured(string method, string path, string stub)
    {
        app.MockPort.Should().Be(ConfiguredMockPort);
        await mock.Send(method, path);
        await mock.ThenAnsweredBy(stub);
    }

    [Then("the admin UI is served at the configured admin address")]
    public async Task AdminAtConfigured()
        => (await app.ClientFor(ConfiguredAdminPort).GetStringAsync("/")).Should().Contain("<title>Pho");

    [Then("nothing is mocked at the configured admin address")]
    public async Task NothingMocked()
    {
        var response = await app.ClientFor(ConfiguredAdminPort).GetAsync("/ping");
        (await response.Content.ReadAsStringAsync()).Should().NotBe("Ping");
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    // ---- reverse proxy -------------------------------------------------------------------------

    [Given("""^Pho is configured to be served under the sub-path "([^"]*)"$""")]
    public void ConfigurePathBase(string pathBase) => app.Settings["Pho:PathBase"] = pathBase;

    [When("""^the admin UI is requested through a proxy that serves it under "([^"]*)"$""")]
    public async Task ThroughAnnouncingProxy(string prefix)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Prefix", prefix);
        _html = await (await app.Admin.SendAsync(request)).EnsureSuccessStatusCode().Content.ReadAsStringAsync();
    }

    [When("the admin UI is requested directly")]
    public async Task Directly() => _html = await app.Admin.GetStringAsync("/");

    [When("""^the admin UI is requested through a proxy that silently removes "([^"]*)"$""")]
    public Task ThroughSilentProxy(string prefix) => Directly();

    [Then("""^every link on the page leads back through "([^"]*)"$""")]
    public void LinksLeadThrough(string prefix)
    {
        var document = new HtmlParser().ParseDocument(_html);
        var baseHref = document.QuerySelector("base")!.GetAttribute("href")!;
        baseHref.Should().Be(prefix);

        var root = new Uri(new Uri("http://proxy.example"), baseHref);
        var links = document.QuerySelectorAll("[href], [src]")
            .Where(e => e.LocalName != "base")
            .Select(e => e.GetAttribute("href") ?? e.GetAttribute("src")!)
            .ToList();

        links.Should().NotBeEmpty();
        foreach (var link in links)
            new Uri(root, link).AbsolutePath.Should().StartWith(prefix, "the link '{0}' must stay under the sub-path", link);
    }

    // ---- persistence and retention -------------------------------------------------------------

    [Given("Pho keeps its data in a database file")]
    public void DatabaseFile()
    {
        var database = Path.Combine(Path.GetTempPath(), $"pho-acceptance-{Guid.NewGuid():N}.db");
        app.ConnectionString = $"Data Source={database}";
        app.OnDispose(() =>
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(database);
        });
    }

    [When("Pho is restarted")]
    public void Restart() => app.Restart();

    [Given("a request was received more than a day ago")]
    public Task ReceivedLongAgo()
        => app.Get<IReceivedRequestLog>().RecordAsync(new ReceivedRequest
        {
            ReceivedAt = DateTime.UtcNow.AddHours(-25),
            Method = "GET",
            Path = "/ancient",
            Outcome = MatchOutcome.NoMatch,
            ResponseStatus = 404
        });
}
