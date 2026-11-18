using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>The Basic auth credential helper in the stub editor (F12).</summary>
[Binding]
public sealed class BasicAuthSteps(Screens ui, Configuration config)
{
    private string? _hoverText;

    [Given("""^the stub "([^"]*)" has a Basic credential for "([^"]*)" with password "([^"]*)", built by the helper$""")]
    public Task GivenCredential(string stub, string userId, string password)
        => config.Change(stub, s =>
        {
            var draft = StubDraft.From(s);
            draft.SetBasicAuth(userId, password);
            draft.ApplyTo(s);
            return s;
        });

    [When("""^I enter the user id "([^"]*)" and the password "([^"]*)" in the basic auth helper$""")]
    public async Task Enter(string userId, string password)
    {
        await ui.Type(ui.AuthHelperField("User ID"), userId);
        await ui.Type(ui.AuthHelperField("Password"), password);
    }

    [When("I apply the basic auth helper")]
    public Task Apply() => Screens.ButtonIn(ui.AuthHelper, "Set Authorization header").ClickAsync(new MouseEventArgs());

    private IReadOnlyList<AngleSharp.Dom.IElement> RulesNamed(string name)
        => ui.Section("Header rules").QuerySelectorAll("tbody tr")
            .Where(tr => string.Equals(tr.QuerySelector("input")?.GetAttribute("value"), name, StringComparison.OrdinalIgnoreCase))
            .ToList();

    [Then("""^the stub has exactly one header rule for "([^"]*)"$""")]
    public void ExactlyOne(string name) => RulesNamed(name).Should().HaveCount(1);

    [Then("""^that rule is a (EQUALS|CONTAINS|REGEX|PRESENT|ABSENT) rule with the value "(.*)"$""")]
    public void RuleIs(string type, string value)
    {
        var row = RulesNamed("Authorization").Single();
        row.QuerySelector("select")!.GetAttribute("value").Should().Be(ArrangeSteps.RuleType(type).ToString());
        row.QuerySelectorAll("input")[1].GetAttribute("value").Should().Be(value);
    }

    [Then("""^the stub's Authorization rule contains the credential "([^"]*)" as literal text$""")]
    public async Task Literal(string credential)
    {
        var rule = (await config.Stubs.ListAsync()).Single().Request.Headers.Single(h => h.Name == "Authorization").Rule;
        rule.Type.Should().Be(MatchRuleType.Regex);
        rule.Value.Should().Contain(Regex.Escape(credential));
        Regex.IsMatch($"Basic {credential}", rule.Value!).Should().BeTrue();
    }

    [When("""^I hover over the value of the header rule "([^"]*)"$""")]
    public void Hover(string name)
        => _hoverText = ui.RuleRow("header", name).QuerySelectorAll("input")[1].GetAttribute("title");

    [Then("""^I read "([^"]*)"$""")]
    public void Read(string text) => _hoverText.Should().Be(text);

    [Then("no decoded reading is offered")]
    public void NoReading()
    {
        _hoverText.Should().BeNullOrEmpty();
        Screens.Text(ui.AuthHelper).Should().NotContain("hover to read it");
    }

    [Then("""^the basic auth helper shows the user id "([^"]*)" and the password "([^"]*)"$""")]
    public void HelperShows(string userId, string password)
    {
        ui.AuthHelperField("User ID").GetAttribute("value").Should().Be(userId);
        ui.AuthHelperField("Password").GetAttribute("value").Should().Be(password);
    }

    [Then("the basic auth helper is empty")]
    public void HelperEmpty() => HelperShows("", "");
}
