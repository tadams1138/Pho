using Bunit;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Moving between views, and the browser's own "leave this page?" safeguard.</summary>
[Binding]
public sealed class NavigationSteps(Screens ui, Browser browser, Configuration config)
{
    [When("I go to the received-requests view")]
    public void GoToReceived() => ui.Navigation.NavigateTo("received");

    [Then("I am still on the stubs screen")]
    public void StillOnStubs() => ui.Navigation.Uri.Should().NotEndWith("/received");

    [Then("I am on the received-requests view")]
    public void OnReceived() => ui.Stubs.WaitForAssertion(() => ui.Navigation.Uri.Should().EndWith("/received"));

    /// <summary>
    /// The stubs screen tells the page whether it holds unsaved edits; the page's own script then
    /// decides whether closing or reloading asks first. Both halves run for real here.
    /// </summary>
    private async Task<bool> BrowserWouldWarn()
    {
        var calls = ui.JS.Invocations["phoSetUnsaved"];
        var unsaved = calls.Count > 0 && (bool)calls.Last().Arguments[0]!;

        var visit = await browser.Open();
        visit.Call("phoSetUnsaved", unsaved);

        return visit.WarnsBeforeLeaving();
    }

    [Then("the browser warns before the page is closed or reloaded")]
    public async Task BrowserWarns() => (await BrowserWouldWarn()).Should().BeTrue();

    [Then("the browser no longer warns before the page is closed or reloaded")]
    public async Task BrowserDoesNotWarn() => (await BrowserWouldWarn()).Should().BeFalse();

    [Then("""^"([^"]*)" has the same request matcher and response as "([^"]*)"$""")]
    public async Task SameDefinition(string copy, string original)
    {
        var a = await config.Stub(copy);
        var b = await config.Stub(original);
        a.Request.Should().BeEquivalentTo(b.Request);
        a.Response.Should().BeEquivalentTo(b.Response);
        a.GroupId.Should().Be(b.GroupId);
    }

    [Then("""^"([^"]*)" is a separate stub from "([^"]*)"$""")]
    public async Task SeparateStub(string copy, string original)
        => (await config.Stub(copy)).Id.Should().NotBe((await config.Stub(original)).Id);
}
