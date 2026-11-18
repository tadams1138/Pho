using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Light and dark theme (F10), in an emulated browser running the page's own script and styles.</summary>
[Binding]
public sealed class ThemeSteps(Browser browser)
{
    [Given("^my system prefers a (light|dark) colour scheme$")]
    [When("^my system switches to a (light|dark) colour scheme$")]
    public void SystemPrefers(string scheme) => browser.SystemPrefersDark = scheme == "dark";

    [Given("I open the admin UI")]
    [When("I open the admin UI")]
    [When("I open the admin UI again later")]
    public Task Open() => browser.Open();

    [Given("^I choose the (light|dark) theme$")]
    [When("^I choose the (light|dark) theme$")]
    public void Choose(string theme) => browser.RequireVisit().ClickButton(theme == "dark" ? "Dark" : "Light");

    [When("I choose to follow the system theme")]
    public void FollowSystem() => browser.RequireVisit().ClickButton("System");

    [Then("^the admin UI is shown in the (light|dark) theme$")]
    public void ShownIn(string theme)
        => browser.RequireVisit().LooksDark.Should().Be(theme == "dark", "the page background resolves to {0}", browser.RequireVisit().Background);
}
