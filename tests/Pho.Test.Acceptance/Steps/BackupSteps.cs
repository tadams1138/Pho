using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Pho.Test.Acceptance.Support;
using Pho.Web.Components.Pages;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Backing up and restoring the configuration from the stubs screen.</summary>
[Binding]
public sealed class BackupSteps(PhoApp app, Screens ui, Configuration config, MockSteps mock)
{
    private HttpResponseMessage? _download;
    private string? _file;

    // ---- export ------------------------------------------------------------------------------

    [When("I export the configuration")]
    [Given("I have exported the configuration")]
    public async Task Export()
    {
        // Follow the Export link the stubs screen offers (relative to wherever the UI is mounted).
        var page = app.Ui.Render<Stubs>();
        var href = page.FindAll("a").Single(a => Screens.Text(a) == "Export").GetAttribute("href")!;
        _download = await app.Admin.GetAsync(href);
        _download.EnsureSuccessStatusCode();
        _file = await _download.Content.ReadAsStringAsync();
    }

    private string File => _file ?? throw new InvalidOperationException("Nothing has been exported.");

    [Given(@"^a client has called (\S+) (\S+)$")]
    public Task ClientCalled(string method, string path) => mock.Send(method, path);

    [Then("I receive a single JSON file")]
    public void JsonFile()
    {
        _download!.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        _download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        _download.Content.Headers.ContentDisposition.FileName.Should().EndWith(".json");
        var parse = () => JsonDocument.Parse(File);
        parse.Should().NotThrow();
    }

    private ConfigSnapshot Exported => JsonSerializer.Deserialize<ConfigSnapshot>(File, Configuration.Json)!;

    [Then("it contains every stub with its full request matcher and response")]
    public async Task ContainsStubs()
        => Exported.Stubs.Should().BeEquivalentTo(await config.Stubs.ListAsync());

    [Then("it contains the whole group tree")]
    public async Task ContainsGroups()
        => Exported.Groups.Should().BeEquivalentTo(await config.Groups.ListAsync());

    [Then("it contains no received requests and no configuration history")]
    public void NothingElse()
    {
        JsonNode.Parse(File)!.AsObject().Select(p => p.Key).Should().BeEquivalentTo("Stubs", "Groups");
        File.Should().NotContain("/health\",\"Query", "no received request should be in the file");
        File.Should().NotContain("Arranged stubs", "no history should be in the file");
    }

    // ---- changing things after the export ----------------------------------------------------

    [Given("the configuration has since changed:")]
    public async Task ChangedSince(DataTable table)
    {
        foreach (var change in table.Rows.Select(r => r["change"]))
        {
            Match m;
            if ((m = Regex.Match(change, "^the stub \"(.+)\" was deleted$")).Success)
            {
                await config.Stubs.DeleteAsync((await config.Stub(m.Groups[1].Value)).Id);
            }
            else if ((m = Regex.Match(change, "^the response body of \"(.+)\" became \"(.*)\"$")).Success)
            {
                var body = m.Groups[2].Value;
                await config.Change(m.Groups[1].Value, s => { s.Response = s.Response with { Body = body }; return s; });
            }
            else if ((m = Regex.Match(change, @"^a stub ""(.+)"" answering (\S+) (\S+) was added$")).Success)
            {
                await config.Stubs.CreateAsync(new Stub
                {
                    Name = m.Groups[1].Value,
                    Request = new RequestMatcher
                    {
                        Method = Enum.Parse<HttpMethodMatch>(m.Groups[2].Value, ignoreCase: true),
                        Path = new PathMatcher(PathMatchType.Exact, m.Groups[3].Value)
                    },
                    Response = new ResponseDefinition { Body = m.Groups[1].Value }
                });
            }
            else if (change == "everything was deleted")
            {
                foreach (var group in (await config.Groups.ListAsync()).Where(g => g.ParentGroupId is null))
                    await config.Groups.DeleteAsync(group.Id);
                foreach (var stub in await config.Stubs.ListAsync())
                    await config.Stubs.DeleteAsync(stub.Id);
            }
            else
            {
                throw new InvalidOperationException($"Unknown change: {change}");
            }

            await config.Record(change);
        }

        (await config.Snapshot()).Should().NotBe(await SnapshotOf(File));
    }

    private async Task<string> SnapshotOf(string file)
    {
        var snapshot = JsonSerializer.Deserialize<ConfigSnapshot>(file, Configuration.Json)!;
        return JsonSerializer.Serialize(new
        {
            stubs = snapshot.Stubs.OrderBy(s => s.Id).ToList(),
            groups = snapshot.Groups.OrderBy(g => g.Id).ToList()
        }, Configuration.Json);
    }

    // ---- import ------------------------------------------------------------------------------

    [Given("an export file written before rules had an ignore-case setting")]
    public async Task OlderFile()
    {
        await Export();
        var json = JsonNode.Parse(File)!;
        foreach (var rule in json.AsObject()["Stubs"]!.AsArray()
                     .SelectMany(s => Rules(s!["Request"]!)))
            rule.AsObject().Remove("IgnoreCase");

        _file = json.ToJsonString();
        _file.Should().NotContain("IgnoreCase");
    }

    private static IEnumerable<JsonNode> Rules(JsonNode request)
    {
        foreach (var list in new[] { "QueryParams", "Headers" })
        foreach (var param in request[list]!.AsArray())
            yield return param!["Rule"]!;

        if (request["Body"] is JsonNode body) yield return body;
    }

    [When("""^I import (the exported file|that file|a file that is not JSON|a JSON file that is not a Pho export|an export file with a stub missing its request), choosing (replace-all|merge)$""")]
    public async Task Import(string which, string mode)
    {
        var content = which switch
        {
            "a file that is not JSON" => "this is not { json",
            "a JSON file that is not a Pho export" => """{ "name": "something else", "items": [1, 2, 3] }""",
            "an export file with a stub missing its request" => await WithoutRequest(),
            _ => File
        };

        await ui.Choose(ui.Stubs.FindAll("label").First(l => Screens.Text(l).StartsWith("Import mode")).QuerySelector("select")!,
            mode == "merge" ? nameof(ImportMode.Merge) : nameof(ImportMode.ReplaceAll));

        var input = ui.Stubs.FindComponent<InputFile>();
        var file = new UploadedFile("pho-mocks.json", content);
        await input.InvokeAsync(() => input.Instance.OnChange.InvokeAsync(new InputFileChangeEventArgs([file])));
    }

    private async Task<string> WithoutRequest()
    {
        await Export();
        var json = JsonNode.Parse(File)!;
        json["Stubs"]![0]!.AsObject().Remove("Request");
        return json.ToJsonString();
    }

    private string? ImportError => ui.Stubs.FindAll(".err").Select(Screens.Text).FirstOrDefault(t => t.StartsWith("Import failed"));

    [Then("the import succeeds")]
    public void Succeeds() => ImportError.Should().BeNull();

    [Then("the import is rejected with an error")]
    public void Rejected() => ImportError.Should().NotBeNullOrWhiteSpace();

    [Then("the configuration is exactly the exported one")]
    public async Task ExactlyExported() => (await config.Snapshot()).Should().Be(await SnapshotOf(File));

    [Then("""^the response body of "([^"]*)" is "([^"]*)" again$""")]
    public async Task BodyAgain(string name, string body) => (await config.Stub(name)).Response.Body.Should().Be(body);

    [Then("""^the stub "([^"]*)" is back, and disabled$""")]
    public async Task BackDisabled(string name) => (await config.Stub(name)).Enabled.Should().BeFalse();

    [Then("every rule has ignore case turned off")]
    public async Task NoIgnoreCase()
    {
        var stubs = await config.Stubs.ListAsync();
        stubs.SelectMany(s => s.Request.Headers.Concat(s.Request.QueryParams).Select(p => p.Rule)
                .Concat(s.Request.Body is null ? [] : [s.Request.Body]))
            .Should().NotBeEmpty().And.OnlyContain(r => !r.IgnoreCase);
    }

    private sealed class UploadedFile(string name, string content) : IBrowserFile
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(content);

        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => _bytes.Length;
        public string ContentType => "application/json";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(_bytes);
    }
}
