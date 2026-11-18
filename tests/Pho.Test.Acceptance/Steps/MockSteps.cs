using System.Net;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>A system under test calling Pho's mock-serving surface, and what it gets back.</summary>
[Binding]
public sealed class MockSteps(PhoApp app, Configuration config, LastExchange last)
{
    public LastExchange Last => last;

    public async Task<HttpResponseMessage> Send(string method, string path, IEnumerable<(string, string)>? headers = null, string? body = null)
    {
        var response = await app.Mock.SendAsync(MockRequests.Build(method, path, headers, body));
        last.Response = response;
        last.Body = await response.Content.ReadAsStringAsync();

        return response;
    }

    private static string WithQuery(string path, string name, string value)
        => path + (path.Contains('?') ? "&" : "?") + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value);

    [When(@"^a client sends (\S+) (\S+)$")]
    public Task WhenAClientSends(string method, string path) => Send(method, path);

    [When("""^a client sends (\S+) (\S+) with (header|query parameter) "([^"]*)" set to "([^"]*)"$""")]
    public Task WhenAClientSendsWith(string method, string path, string kind, string name, string value)
        => kind == "header" ? Send(method, path, [(name, value)]) : Send(method, WithQuery(path, name, value));

    [When("""^a client sends (\S+) (\S+) with body "(.*)"$""")]
    public Task WhenAClientSendsBody(string method, string path, string body) => Send(method, path, body: body);

    [When(@"^a client sends (\S+) (\S+) with these headers:$")]
    public Task WhenAClientSendsHeaders(string method, string path, DataTable table)
        => Send(method, path, table.Rows.Select(r => (r["name"], r["value"])).ToList());

    [Then("""^the request is answered by "([^"]*)"$""")]
    public async Task ThenAnsweredBy(string name)
    {
        var stub = await config.Stub(name);
        var response = last.RequireResponse();
        ((int)response.StatusCode).Should().Be(stub.Response.Status, "the stub '{0}' should have answered, but got: {1}", name, last.Body);
        last.Body.Should().Be(stub.Response.Body);
    }

    [Then("""^the request is answered by (no stub|[^"]\S*)$""")]
    public Task ThenAnsweredByUnquoted(string answer)
    {
        if (answer != "no stub") return ThenAnsweredBy(answer);

        ThenNoStubMatched();
        return Task.CompletedTask;
    }

    [Then("the response body is not the response of either stub")]
    public async Task ThenNotEitherStub()
    {
        foreach (var stub in await config.Stubs.ListAsync()) last.Body.Should().NotBe(stub.Response.Body);
    }

    [Then("""^an error is logged naming "([^"]*)", "([^"]*)" and the request "(\S+) (\S+)"$""")]
    public void ThenErrorLogged(string first, string second, string method, string path)
        => app.Logs.Entries.Should().Contain(e =>
            e.Level == Microsoft.Extensions.Logging.LogLevel.Error
            && e.Message.Contains(first) && e.Message.Contains(second)
            && e.Message.Contains(method) && e.Message.Contains(path));

    [Then("the caller is told that no stub matched")]
    public void ThenNoStubMatched()
    {
        last.RequireResponse().StatusCode.Should().Be(HttpStatusCode.NotFound, "body: {0}", last.Body);
        last.Body.Should().Contain("No stub matched");
    }

    [Then("""^the caller receives a server error naming "([^"]*)" and "([^"]*)"$""")]
    public void ThenServerErrorNaming(string first, string second)
    {
        last.RequireResponse().StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        last.Body.Should().Contain(first).And.Contain(second);
    }

    [Then(@"^the caller receives status (\d+)$")]
    public void ThenStatus(int status) => ((int)last.RequireResponse().StatusCode).Should().Be(status, "body: {0}", last.Body);

    [Then("""^the response body is "(.*)"$""")]
    public void ThenBody(string body) => last.Body.Should().Be(body);

    [Then("""^the response carries header "([^"]*)" with value "([^"]*)"$""")]
    public void ThenHeader(string name, string value)
    {
        var response = last.RequireResponse();
        var values = response.Headers.TryGetValues(name, out var h) ? h
            : response.Content.Headers.TryGetValues(name, out var c) ? c
            : null;
        values.Should().NotBeNull("the response should carry a '{0}' header", name);
        string.Join(", ", values!).Should().Be(value);
    }

    [Then("the response carries no headers of its own")]
    public void ThenNoHeaders()
    {
        var response = last.RequireResponse();
        var names = response.Headers.Select(h => h.Key).Concat(response.Content.Headers.Select(h => h.Key))
            // Framing the transport adds by itself, not anything a stub asked for.
            .Where(n => n is not ("Content-Length" or "Date" or "Server" or "Transfer-Encoding"));
        names.Should().BeEmpty();
    }

    [Then("""^a client sending (\S+) (\S+) receives "(.*)"$""")]
    public async Task ThenAClientReceives(string method, string path, string body)
    {
        await Send(method, path);
        last.Body.Should().Be(body);
    }

    [Then("""^a client sending (\S+) (\S+) with (header|query parameter) "([^"]*)" set to "([^"]*)" receives "([^"]*)"$""")]
    public async Task ThenAClientReceivesWith(string method, string path, string kind, string name, string value, string body)
    {
        await WhenAClientSendsWith(method, path, kind, name, value);
        last.Body.Should().Be(body);
    }

    [Then("a client sending (\\S+) (\\S+) is told that no stub matched")]
    public async Task ThenAClientIsToldNoMatch(string method, string path)
    {
        await Send(method, path);
        ThenNoStubMatched();
    }

    /// <summary>
    /// Sends one request per row and checks who answered: a stub's name, "no stub", or "ambiguous".
    /// </summary>
    [Then("requests are answered like this:")]
    public async Task ThenRequestsAreAnsweredLikeThis(DataTable table)
    {
        foreach (var row in table.Rows)
        {
            var headers = MockRequests.ParseHeaders(row.TryGetValue("headers", out var h) ? h : null).ToList();
            var body = row.TryGetValue("body", out var b) && b.Length > 0 ? b : null;
            await Send(row["method"], row["path"], headers, body);

            var because = $"{row["method"]} {row["path"]} [{string.Join("; ", headers.Select(x => x.Item1 + ": " + x.Item2))}] body '{body}' should be answered by {row["answer"]}";
            switch (row["answer"])
            {
                case "no stub":
                    last.RequireResponse().StatusCode.Should().Be(HttpStatusCode.NotFound, because);
                    break;
                case "ambiguous":
                    last.RequireResponse().StatusCode.Should().Be(HttpStatusCode.InternalServerError, because);
                    break;
                default:
                    var stub = await config.Stub(row["answer"]);
                    ((int)last.RequireResponse().StatusCode).Should().Be(stub.Response.Status, because + " (got: " + last.Body + ")");
                    last.Body.Should().Be(stub.Response.Body, because);
                    break;
            }
        }
    }
}
