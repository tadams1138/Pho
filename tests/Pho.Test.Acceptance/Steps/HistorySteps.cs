using Microsoft.EntityFrameworkCore;
using Pho.Infrastructure;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Configuration changes made in the UI, and stepping back and forth through them.</summary>
[Binding]
public sealed class HistorySteps(Screens ui, Configuration config, PhoApp app, EditorSteps editor, TreeSteps tree)
{
    private string? _noted;

    [Given("I note the configuration")]
    [When("I note the configuration")]
    public async Task Note() => _noted = await config.Snapshot();

    [Then("the configuration has changed")]
    public async Task Changed() => (await config.Snapshot()).Should().NotBe(_noted);

    [Then("the configuration is as I noted it")]
    public async Task AsNoted() => (await config.Snapshot()).Should().Be(_noted);

    [When("I undo")]
    public Task Undo() => ui.Click("Undo");

    [When("I redo")]
    public Task Redo() => ui.Click("Redo");

    [When("I undo as far as possible")]
    public async Task UndoAll()
    {
        for (var i = 0; i < 100 && Screens.IsOffered(ui.Button("Undo")); i++) await Undo();
    }

    [Then("^(undo|redo) is (offered|not offered)$")]
    public void Offered(string action, string offered)
        => Screens.IsOffered(ui.Button(action == "undo" ? "Undo" : "Redo")).Should().Be(offered == "offered");

    [Then("no change has been recorded")]
    public async Task NoChangeRecorded()
        => (await app.Get<IConfigHistoryStore>().GetCurrentSequenceAsync()).Should().Be(config.ArrangedSequence);

    // ---- changes, made the way a user makes them ----------------------------------------------

    [When(@"^I create a stub ""([^""]*)"" answering (\S+) (\S+)$")]
    public async Task CreateStub(string name, string method, string path)
    {
        await editor.StartNewStub();
        await editor.SetText("name", name);
        await editor.SetMethod(method);
        await editor.SetText("path", path);
        await editor.Save();
    }

    [When("""^I change the response body of "([^"]*)" to "([^"]*)"$""")]
    public async Task ChangeBody(string name, string body)
    {
        await tree.Select(name);
        await editor.SetResponseBody(body);
        await editor.Save();
    }

    [When("""^I delete the (?:stub|group) "([^"]*)"$""")]
    public async Task Delete(string label)
    {
        await tree.Select(label);
        await tree.DeleteSelection();
        await tree.ConfirmDeletion();
    }

    [When("""^I disable the stub "([^"]*)"$""")]
    public async Task Disable(string name)
    {
        await tree.Select(name);
        await tree.DisableSelection();
    }

    [When("""^I move the (?:stub|group) "([^"]*)" into "([^"]*)"$""")]
    public Task Move(string label, string group) => ui.Drag(label, group);

    [When("""^I move the (?:stub|group) "([^"]*)" to the top level$""")]
    public Task MoveToTop(string label) => ui.Drag(label, null);

    // ---- retention ----------------------------------------------------------------------------

    [Given(@"^a stub ""([^""]*)"" answering (\S+) (\S+) was created more than a year ago$")]
    public async Task CreatedLongAgo(string name, string method, string path)
    {
        await CreateDirectly(name, method, path);

        // Everything in history so far happened 400 days ago.
        var db = app.Get<PhoDbContext>();
        await db.ConfigRevisions.ExecuteUpdateAsync(r => r.SetProperty(x => x.CreatedAt, DateTime.UtcNow.AddDays(-400)));
    }

    [Given(@"^a stub ""([^""]*)"" answering (\S+) (\S+) was created today$")]
    public Task CreatedToday(string name, string method, string path) => CreateDirectly(name, method, path);

    private async Task CreateDirectly(string name, string method, string path)
    {
        await config.Stubs.CreateAsync(new Stub
        {
            Name = name,
            Request = new RequestMatcher
            {
                Method = Enum.Parse<HttpMethodMatch>(method, ignoreCase: true),
                Path = new PathMatcher(PathMatchType.Exact, path)
            },
            Response = new ResponseDefinition { Body = name }
        });
        await config.Record($"Created '{name}'");
    }
}
