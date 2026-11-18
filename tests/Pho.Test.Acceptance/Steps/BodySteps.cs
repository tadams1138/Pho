using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Body editing helpers (F8) and spell-check hints (F9).</summary>
[Binding]
public sealed class BodySteps(Screens ui, Configuration config)
{
    private const string Fields = "(response body|request body value)";

    private static string Caption(string field) => field == "response body" ? "Response body" : "Request body value";

    [When($"^I type '(.*)' into the {Fields}$")]
    public Task TypeInto(string text, string field) => ui.Type(ui.BodyField(Caption(field)), text);

    [When($"^I format the {Fields} as (JSON|XML)$")]
    public Task Format(string field, string format)
        => Screens.ButtonIn(ui.BodyTools(Caption(field)), $"Format {format}").ClickAsync(new MouseEventArgs());

    [When($"^I validate the {Fields} as (JSON|XML)$")]
    public Task Validate(string field, string format)
        => Screens.ButtonIn(ui.BodyTools(Caption(field)), $"Validate {format}").ClickAsync(new MouseEventArgs());

    [Then($"^the {Fields} reads:$")]
    public void Reads(string field, string expected)
    {
        var actual = ui.BodyField(Caption(field)).GetAttribute("value") ?? "";
        Normalise(actual).Should().Be(Normalise(expected.Replace("\\n", "\n")));
    }

    private static string Normalise(string s) => s.Replace("\r\n", "\n");

    private string Verdict(string field)
        => ui.BodyTools(Caption(field)).QuerySelector(".ok, .err") is { } verdict ? Screens.Text(verdict) : "";

    [Then($"^the {Fields} is reported as well-formed$")]
    public void WellFormed(string field)
    {
        ui.BodyTools(Caption(field)).QuerySelector(".ok").Should().NotBeNull("got: {0}", Verdict(field));
        Verdict(field).Should().Be("Valid.");
    }

    [Then($"^the {Fields} is reported as malformed, with the error's location$")]
    public void Malformed(string field)
    {
        ui.BodyTools(Caption(field)).QuerySelector(".err").Should().NotBeNull("got: {0}", Verdict(field));
        // JSON errors give a line and byte position, XML errors a line and position.
        Verdict(field).Should().MatchRegex("(?i)line.*position");
    }

    [Then("these fields offer spell-check hints:")]
    public void SpellCheck(DataTable table)
    {
        foreach (var field in table.Rows.Select(r => r["field"]))
        {
            var element = field switch
            {
                "name" => ui.Field("Name"),
                "description" => ui.Field("Description"),
                _ => ui.BodyField(Caption(field))
            };
            element.GetAttribute("spellcheck").Should().Be("true", "the {0} should be spell-checked", field);
        }
    }

    [Then("""^its description is "([^"]*)"$""")]
    public async Task Description(string description)
        => (await config.Stubs.ListAsync()).Single().Description.Should().Be(description);
}
