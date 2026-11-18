using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Pho.Test.Acceptance.Support;
using Reqnroll;

namespace Pho.Test.Acceptance.Steps;

/// <summary>The stub tree: selecting, arranging, deleting, expanding and collapsing.</summary>
[Binding]
public sealed class TreeSteps(Screens ui, Configuration config)
{
    [When("""^I select "([^"]*)"$""")]
    public Task Select(string label) => ui.ClickRow(label);

    [When("""^I ctrl-click "([^"]*)"$""")]
    public Task CtrlClick(string label) => ui.ClickRow(label, ctrl: true);

    [When("""^I shift-click "([^"]*)"$""")]
    public Task ShiftClick(string label) => ui.ClickRow(label, shift: true);

    [When("""^I tick the checkbox of "([^"]*)"$""")]
    public Task Tick(string label) => ui.TickRow(label);

    [When("I expand all groups")]
    [Given("the whole tree is expanded")]
    public Task ExpandAll() => ui.Click("Expand all");

    [When("I collapse all groups")]
    public Task CollapseAll() => ui.Click("Collapse all");

    [When("I delete the selection")]
    public Task DeleteSelection() => ui.Click("Delete selected");

    [When("I confirm the deletion")]
    public Task ConfirmDeletion() => Screens.ButtonIn(RequireDeletePrompt(), "Yes, delete").ClickAsync(new MouseEventArgs());

    [When("I cancel the deletion")]
    public Task CancelDeletion() => Screens.ButtonIn(RequireDeletePrompt(), "Cancel").ClickAsync(new MouseEventArgs());

    private AngleSharp.Dom.IElement RequireDeletePrompt()
        => ui.DeletePrompt ?? throw new InvalidOperationException("No delete confirmation is shown.");

    [Then("""^I am asked to confirm deleting "([^"]*)"$""")]
    public void AskedToConfirm(string what) => Screens.Text(RequireDeletePrompt()).Should().Contain($"Delete {what}?");

    [Then("I am warned that a group is deleted with everything inside it")]
    public void WarnedCascade() => Screens.Text(RequireDeletePrompt()).Should().Contain("A group is deleted with everything inside it");

    [Then("no delete confirmation is shown")]
    public void NoDeletePrompt() => ui.DeletePrompt.Should().BeNull();

    [When("""^I drag "([^"]*)" onto "([^"]*)"$""")]
    public Task Drag(string label, string target) => ui.Drag(label, target);

    [When("""^I drag "([^"]*)" to the top level$""")]
    public Task DragToTop(string label) => ui.Drag(label, null);

    [When("""^I add a group "([^"]*)"$""")]
    public async Task AddGroup(string name)
    {
        await ui.Stubs.Find("input[placeholder='New group name']").ChangeAsync(new ChangeEventArgs { Value = name });
        await ui.Choose(ParentPicker(), "");
        await ui.Click("Add group");
    }

    [When("""^I add a group "([^"]*)" inside "([^"]*)"$""")]
    public async Task AddGroupInside(string name, string parent)
    {
        var parentId = (await config.Group(parent)).Id;
        await ui.Stubs.Find("input[placeholder='New group name']").ChangeAsync(new ChangeEventArgs { Value = name });
        await ui.Choose(ParentPicker(), parentId.ToString());
        await ui.Click("Add group");
    }

    private AngleSharp.Dom.IElement ParentPicker()
        => ui.Stubs.FindAll(".toolbar label").First(l => Screens.Text(l).StartsWith("in")).QuerySelector("select")!;

    [When("""^I rename the group "([^"]*)" to "([^"]*)"$""")]
    public async Task Rename(string group, string name)
    {
        await ui.ClickRow(group);
        var rename = ui.FindButton("Rename")
                     ?? throw new InvalidOperationException("The stubs screen offers no way to rename a group.");
        await rename.ClickAsync(new MouseEventArgs());
        await ui.Type(ui.Stubs.Find("input[placeholder='Group name']"), name);
    }

    [Then("""^the stub tree shows "([^"]*)"$""")]
    public void TreeShows(string label) => ui.VisibleLabels.Should().Contain(label);

    [Then("""^the stub tree does not show "([^"]*)"$""")]
    public void TreeDoesNotShow(string label) => ui.VisibleLabels.Should().NotContain(label);

    [Then("the stub tree shows exactly:")]
    public void TreeShowsExactly(DataTable table)
    {
        var expected = table.Rows.Select(r => (r["row"], int.Parse(r["level"]))).ToList();
        var actual = ui.Rows.Select(r => (Screens.LabelOf(r), Screens.DepthOf(r))).ToList();
        actual.Should().Equal(expected);
    }

    [Then("""^"([^"]*)" is shown inside "([^"]*)"$""")]
    public void ShownInside(string label, string group) => ui.ParentOf(label).Should().Be(group);

    [Then("""^"([^"]*)" is shown at the top level$""")]
    public void ShownAtTop(string label) => ui.ParentOf(label).Should().BeNull();

    [Then("""^"([^"]*)" is marked disabled$""")]
    public void MarkedDisabled(string label) => ui.IsMarkedDisabled(label).Should().BeTrue();

    [Then("""^"([^"]*)" is not marked disabled$""")]
    public void NotMarkedDisabled(string label) => ui.IsMarkedDisabled(label).Should().BeFalse();

    [Then("the selection is:")]
    public void SelectionIs(DataTable table)
        => ui.SelectedLabels.Should().BeEquivalentTo(table.Rows.Select(r => r["row"]));

    [Then("nothing is selected")]
    public void NothingSelected() => ui.SelectedLabels.Should().BeEmpty();

    [Then("no row carries a delete button of its own")]
    public void NoRowDeleteButtons()
        => ui.Rows.SelectMany(r => r.QuerySelectorAll("button")).Should().OnlyContain(b => b.ClassList.Contains("twisty"));

    [Then("""^the group "([^"]*)" is (expanded|collapsed)$""")]
    public void GroupState(string group, string state) => ui.IsExpanded(group).Should().Be(state == "expanded");

    // ---- enable / disable --------------------------------------------------------------------

    [When("I disable the selection")]
    public Task DisableSelection() => ui.Click("Disable");

    [When("I enable the selection")]
    public Task EnableSelection() => ui.Click("Enable");

    [Then("^the (enable|disable) action is (offered|not offered)$")]
    public void ActionOffered(string action, string offered)
        => Screens.IsOffered(ui.Button(action == "enable" ? "Enable" : "Disable")).Should().Be(offered == "offered");

    [Then("""^the stub "([^"]*)" is (enabled|disabled)$""")]
    [Then("""^"([^"]*)" is (enabled|disabled)$""")]
    public async Task StubState(string name, string state) => (await config.Stub(name)).Enabled.Should().Be(state == "enabled");
}
