using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>Operating the stub tree from the keyboard (F11).</summary>
[Binding]
public sealed class KeyboardSteps(Screens ui, Configuration config, TreeSteps tree)
{
    private static string Key(string name) => name switch
    {
        "Down" => "ArrowDown",
        "Up" => "ArrowUp",
        "Left" => "ArrowLeft",
        "Right" => "ArrowRight",
        "Space" => " ",
        _ => name
    };

    [When("""^I (?:focus the tree and )?press "([^"]*)"$""")]
    public Task Press(string key) => ui.Press(Key(key));

    [When("""^I press "([^"]*)" (\d+) times$""")]
    public async Task PressTimes(string key, int times)
    {
        for (var i = 0; i < times; i++) await Press(key);
    }

    [Given("""^the active row is "([^"]*)"$""")]
    public Task GivenActive(string label) => ui.ClickRow(label);

    [Then("""^the active row is "([^"]*)"$""")]
    public void ThenActive(string label)
    {
        ui.IsActive(label).Should().BeTrue("'{0}' should be the active row", label);
        ui.Rows.Count(r => r.ClassList.Contains("active")).Should().Be(1);
    }

    [Then("""^"([^"]*)" has been scrolled into view$""")]
    public async Task ScrolledIntoView(string label)
    {
        var id = (await config.GroupOrNull(label))?.Id ?? (await config.Stub(label)).Id;
        ui.JS.Invocations["phoTree.reveal"].Select(i => i.Arguments[0]).Should().Contain($"tree-row-{id}");
    }

    [When("""^I select "([^"]*)" and ctrl-click "([^"]*)"$""")]
    public async Task SelectAndCtrlClick(string first, string second)
    {
        await tree.Select(first);
        await tree.CtrlClick(second);
    }
}
