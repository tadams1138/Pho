using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Working in the stub editor.</summary>
[Binding]
public sealed class EditorSteps(Screens ui, Configuration config)
{
    [Given("I am on the stubs screen")]
    [When("I open the stubs screen")]
    public void OpenStubsScreen() => _ = ui.Stubs;

    [StepDefinition("I start a new stub")]
    public Task StartNewStub() => ui.Click("New stub");

    [When("""^I set the (name|description|path) to "([^"]*)"$""")]
    public Task SetText(string field, string value) => ui.Type(ui.Field(Capitalise(field)), value);

    [When("I clear the path")]
    public Task ClearPath() => ui.Type(ui.Field("Path"), "");

    [When("I clear the response status")]
    public Task ClearStatus() => ui.Type(ui.Field("Status"), "");

    [When("""^I set the method to "([^"]*)"$""")]
    public Task SetMethod(string method)
        => ui.Choose(ui.Field("Method"), Enum.Parse<HttpMethodMatch>(method, ignoreCase: true).ToString());

    [When("""^I set the path match type to "([^"]*)"$""")]
    public Task SetPathType(string type)
        => ui.Choose(ui.Field("Path match type"), Enum.Parse<PathMatchType>(type, ignoreCase: true).ToString());

    [When(@"^I set the response status to (\d+)$")]
    public Task SetStatus(int status) => ui.Type(ui.Field("Status"), status.ToString());

    [When("""^I set the response body to "([^"]*)"$""")]
    public Task SetResponseBody(string body) => ui.Type(ui.BodyField("Response body"), body);

    [When("""^I add a (header|query parameter) rule "([^"]*)" (equals|contains|regex|present|absent)((?: "[^"]*")?)$""")]
    public async Task AddRule(string kind, string name, string type, string quotedValue)
    {
        await Screens.ButtonIn(ui.Section(Screens.RulesSection(kind)), $"Add {kind} rule").ClickAsync(new MouseEventArgs());
        await ui.Type(ui.LastRuleRow(kind).QuerySelector("input")!, name);
        await ui.Choose(ui.RuleRow(kind, name).QuerySelector("select")!, ArrangeSteps.RuleType(type).ToString());
        if (ArrangeSteps.OptionalQuoted(quotedValue) is { } value)
            await ui.Type(ui.RuleRow(kind, name).QuerySelectorAll("input")[1], value);
    }

    [When("""^I turn on ignore case for the (header|query parameter) rule "([^"]*)"$""")]
    public Task IgnoreCaseOnRule(string kind, string name)
        => ui.SetChecked(IgnoreCaseBox(kind, name) ?? throw new InvalidOperationException("No ignore-case control."), true);

    private AngleSharp.Dom.IElement? IgnoreCaseBox(string kind, string name)
        => ui.RuleRow(kind, name).QuerySelector("input[type=checkbox][title='Ignore case']");

    [StepDefinition("""^I add a body rule (equals|contains|regex|present|absent)((?: "[^"]*")?)$""")]
    public async Task AddBodyRule(string type, string quotedValue)
    {
        await ui.SetChecked(ui.Checkbox("Match on the request body"), true);
        await ui.Choose(ui.Field("Body rule"), ArrangeSteps.RuleType(type).ToString());
        if (ArrangeSteps.OptionalQuoted(quotedValue) is { } value) await ui.Type(ui.BodyField("Request body value"), value);
    }

    [When("I turn on ignore case for the body rule")]
    public Task IgnoreCaseOnBody() => ui.SetChecked(ui.Checkbox("Ignore case"), true);

    [Then("""^ignore case is off for the (header|query parameter) rule "([^"]*)"$""")]
    public void IgnoreCaseIsOff(string kind, string name)
    {
        var box = IgnoreCaseBox(kind, name);
        box.Should().NotBeNull("an ignore-case control should be offered");
        box!.HasAttribute("checked").Should().BeFalse();
    }

    [Then("""^no ignore-case control is offered for the (header|query parameter) rule "([^"]*)"$""")]
    public void NoIgnoreCaseOnRule(string kind, string name) => IgnoreCaseBox(kind, name).Should().BeNull();

    [Then("no ignore-case control is offered for the body rule")]
    public void NoIgnoreCaseOnBody() => ui.CheckboxOrNull("Ignore case").Should().BeNull();

    [When("""^I add a response header "([^"]*)" with value "([^"]*)"$""")]
    public async Task AddResponseHeader(string name, string value)
    {
        var section = ui.Section("Response headers");
        await Screens.ButtonIn(section, "Add response header").ClickAsync(new MouseEventArgs());
        var row = ui.Section("Response headers").QuerySelectorAll("tbody tr").Last();
        await ui.Type(row.QuerySelectorAll("input")[0], name);
        row = ui.Section("Response headers").QuerySelectorAll("tbody tr").Last();
        await ui.Type(row.QuerySelectorAll("input")[1], value);
    }

    [When("I save the stub")]
    public Task Save() => ui.Click("Save");

    [When("I duplicate the stub")]
    public Task Duplicate() => ui.Click("Duplicate");

    [Then("""^the name field suggests "([^"]*)"$""")]
    public void NameSuggests(string name)
    {
        ui.Field("Name").GetAttribute("value").Should().BeNullOrEmpty();
        ui.Field("Name").GetAttribute("placeholder").Should().Be(name);
    }

    [Then("""^saving is refused with the message "([^"]*)"$""")]
    public void SavingRefused(string message) => ui.Errors.Should().Contain(message);

    [Then("saving is refused with a message about the response status")]
    public void SavingRefusedStatus() => ui.Errors.Should().Contain(e => e.Contains("status", StringComparison.OrdinalIgnoreCase));

    [Then("""^a stub named "([^"]*)" is stored$""")]
    public async Task StubIsStored(string name) => (await config.StubOrNull(name)).Should().NotBeNull();

    [Then("no stub is stored")]
    public async Task NoStubStored() => (await config.Stubs.ListAsync()).Should().BeEmpty();

    [Then("""^the editor shows the stub "([^"]*)"$""")]
    public void EditorShows(string name)
    {
        ui.EditorIsOpen.Should().BeTrue("the editor should be open");
        ui.EditorTitle.Should().Be(name);
    }

    [Then("""^the editor's path is "([^"]*)"$""")]
    public void EditorPath(string path) => ui.Field("Path").GetAttribute("value").Should().Be(path);

    [Then("the editor shows unsaved changes")]
    public void EditorDirty() => ui.DirtyFlag.Should().Contain("Unsaved changes");

    [Then("the editor shows the stub as saved")]
    public void EditorClean() => ui.DirtyFlag.Should().Be("Saved");

    [Then("no editor is shown")]
    public void NoEditor() => ui.EditorIsOpen.Should().BeFalse();

    [Then("I am warned about unsaved changes")]
    public void WarnedUnsaved() => ui.UnsavedPrompt.Should().NotBeNull("an unsaved-changes warning should be shown");

    [When("^I choose to (save|discard|stay)$")]
    public Task Choose(string choice)
    {
        var prompt = ui.UnsavedPrompt ?? throw new InvalidOperationException("No unsaved-changes warning is shown.");
        var button = choice switch
        {
            "save" => "Save and continue",
            "discard" => "Discard changes",
            _ => "Cancel"
        };

        return Screens.ButtonIn(prompt, button).ClickAsync(new MouseEventArgs());
    }

    private static string Capitalise(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
