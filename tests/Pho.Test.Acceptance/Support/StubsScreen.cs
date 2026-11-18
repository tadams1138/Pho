using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Pho.Web.Components.Pages;

namespace Pho.Test.Acceptance.Support;

/// <summary>
/// Drives Pho's admin screens the way a user would — finding things by what they say on screen,
/// clicking, typing and pressing keys — through bUnit, against the running app's services.
/// All knowledge of markup lives here, so the steps (and the feature files) stay free of it.
/// </summary>
public sealed class Screens(PhoApp app)
{
    private IRenderedComponent<Stubs>? _stubs;
    private IRenderedComponent<ReceivedRequests>? _received;

    public IRenderedComponent<Stubs> Stubs
    {
        get
        {
            if (_stubs is null)
            {
                _stubs = app.Ui.Render<Stubs>();
                _stubs.WaitForState(() => !_stubs.Markup.Contains("Loading…"));
            }

            return _stubs;
        }
    }

    public bool StubsScreenIsOpen => _stubs is not null;

    public void ReopenStubs()
    {
        _stubs = null;
        _ = Stubs;
    }

    public IRenderedComponent<ReceivedRequests> Received
    {
        get
        {
            if (_received is null)
            {
                _received = app.Ui.Render<ReceivedRequests>();
                _received.WaitForState(() => !_received.Markup.Contains("Loading…"));
            }

            return _received;
        }
    }

    public void ReopenReceived()
    {
        _received = null;
        _ = Received;
    }

    public BunitJSInterop JS => app.Ui.JSInterop;

    public NavigationManager Navigation => app.Ui.Services.GetRequiredService<NavigationManager>();

    // ---- generic -----------------------------------------------------------------------------

    public static string Text(INode node) => string.Join(' ', node.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static IElement ButtonIn(IElement scope, string text)
        => scope.QuerySelectorAll("button").FirstOrDefault(b => Text(b).StartsWith(text, StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"No '{text}' button. Buttons: {string.Join(", ", scope.QuerySelectorAll("button").Select(Text))}");

    public IElement? FindButton(string text)
        => Stubs.FindAll("button").FirstOrDefault(b => Text(b).StartsWith(text, StringComparison.Ordinal));

    public IElement Button(string text)
        => FindButton(text) ?? throw new InvalidOperationException(
            $"No '{text}' button on the stubs screen. Buttons: {string.Join(", ", Stubs.FindAll("button").Select(Text))}");

    public static bool IsOffered(IElement button) => !button.HasAttribute("disabled");

    public Task Click(string buttonText) => Button(buttonText).ClickAsync(new MouseEventArgs());

    // ---- tree --------------------------------------------------------------------------------

    public IReadOnlyList<IElement> Rows => Stubs.FindAll(".tree-row");

    public static bool IsGroup(IElement row) => row.ClassList.Contains("row-group");

    public static string LabelOf(IElement row)
        => IsGroup(row)
            ? Text(row.QuerySelector(".group")!).Replace("📁", "").Trim()
            : Text(row.QuerySelector(".stub-label")!);

    /// <summary>How deeply a row is nested, as its indentation shows it.</summary>
    public static int DepthOf(IElement row)
    {
        var style = row.GetAttribute("style") ?? "";
        var rem = double.Parse(style.Split(':')[1].Replace("rem", ""), CultureInfo.InvariantCulture);

        return (int)Math.Round((rem - 0.4) / 1.1);
    }

    public IElement? RowOrNull(string label) => Rows.FirstOrDefault(r => LabelOf(r) == label);

    public IElement Row(string label)
        => RowOrNull(label) ?? throw new InvalidOperationException(
            $"No row '{label}' is visible. Visible rows: {string.Join(", ", Rows.Select(LabelOf))}");

    public IReadOnlyList<string> VisibleLabels => Rows.Select(LabelOf).ToList();

    public string? ParentOf(string label)
    {
        var rows = Rows;
        var index = rows.ToList().FindIndex(r => LabelOf(r) == label);
        if (index < 0) throw new InvalidOperationException($"No row '{label}' is visible.");

        var depth = DepthOf(rows[index]);
        for (var i = index - 1; i >= 0; i--)
        {
            if (DepthOf(rows[i]) == depth - 1 && IsGroup(rows[i])) return LabelOf(rows[i]);
        }

        return null;
    }

    public bool IsSelected(string label) => Row(label).GetAttribute("aria-selected") == "true";

    public bool IsActive(string label) => Row(label).ClassList.Contains("active");

    public bool IsExpanded(string groupLabel)
        => Row(groupLabel).QuerySelector("button.twisty")!.GetAttribute("aria-label")!.StartsWith("Collapse");

    public bool IsMarkedDisabled(string label) => Row(label).ClassList.Contains("disabled");

    public IReadOnlyList<string> SelectedLabels => Rows.Where(r => r.GetAttribute("aria-selected") == "true").Select(LabelOf).ToList();

    public Task ClickRow(string label, bool ctrl = false, bool shift = false)
        => Row(label).ClickAsync(new MouseEventArgs { CtrlKey = ctrl, ShiftKey = shift });

    public Task TickRow(string label)
        => Row(label).QuerySelector("input.row-select")!.ChangeAsync(new ChangeEventArgs { Value = true });

    public Task Press(string key) => Stubs.Find(".tree").KeyDownAsync(new KeyboardEventArgs { Key = key });

    public async Task Drag(string label, string? ontoLabel)
    {
        await Row(label).DragStartAsync(new DragEventArgs());
        var target = ontoLabel is null ? Stubs.Find(".tree-root-drop") : Row(ontoLabel);
        await target.DropAsync(new DragEventArgs());
    }

    // ---- prompts -----------------------------------------------------------------------------

    public IElement? DeletePrompt => Stubs.FindAll(".confirm-bar").FirstOrDefault(e => !e.ClassList.Contains("unsaved"));

    public IElement? UnsavedPrompt => Stubs.FindAll(".confirm-bar.unsaved").FirstOrDefault();

    // ---- editor ------------------------------------------------------------------------------

    public bool EditorIsOpen => Stubs.FindAll(".editor-header").Count > 0;

    public string EditorTitle => Text(Stubs.Find(".editor-header h2"));

    public string DirtyFlag => Text(Stubs.Find(".dirty-flag"));

    public IReadOnlyList<string> Errors => Stubs.FindAll(".errors li").Select(Text).ToList();

    private static string OwnText(IElement element)
        => string.Concat(element.ChildNodes.Where(n => n.NodeType == NodeType.Text).Select(n => n.TextContent)).Trim();

    /// <summary>The input, select or textarea labelled <paramref name="label"/> in the editor.</summary>
    public IElement Field(string label)
        => Stubs.FindAll(".editor-pane label").FirstOrDefault(l => OwnText(l) == label)?.QuerySelector("input, select, textarea")
           ?? throw new InvalidOperationException($"No editor field labelled '{label}'.");

    /// <summary>The textarea following a caption such as "Response body".</summary>
    public IElement BodyField(string caption)
        => Stubs.FindAll(".editor-pane div").FirstOrDefault(d => OwnText(d) == caption && d.Children.Length == 0)?.NextElementSibling
           ?? throw new InvalidOperationException($"No body field '{caption}'.");

    public IElement BodyTools(string caption) => BodyField(caption).NextElementSibling!;

    public Task Type(IElement field, string value) => field.InputAsync(new ChangeEventArgs { Value = value });

    public Task Choose(IElement select, string value) => select.ChangeAsync(new ChangeEventArgs { Value = value });

    public Task SetChecked(IElement checkbox, bool value) => checkbox.ChangeAsync(new ChangeEventArgs { Value = value });

    /// <summary>The checkbox whose label reads <paramref name="label"/>.</summary>
    public IElement Checkbox(string label)
        => Stubs.FindAll(".editor-pane label").FirstOrDefault(l => Text(l) == label)?.QuerySelector("input[type=checkbox]")
           ?? throw new InvalidOperationException($"No checkbox labelled '{label}'.");

    public IElement? CheckboxOrNull(string label)
        => Stubs.FindAll(".editor-pane label").FirstOrDefault(l => Text(l) == label)?.QuerySelector("input[type=checkbox]");

    public IElement Section(string summaryStart)
        => Stubs.FindAll(".editor-pane details.section")
               .FirstOrDefault(d => Text(d.QuerySelector("summary")!).StartsWith(summaryStart, StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"No editor section '{summaryStart}'.");

    public static string RulesSection(string kind) => kind == "header" ? "Header rules" : "Query parameter rules";

    /// <summary>The rule row for <paramref name="name"/> among the header or query parameter rules.</summary>
    public IElement RuleRow(string kind, string name)
        => Section(RulesSection(kind)).QuerySelectorAll("tbody tr")
               .FirstOrDefault(tr => tr.QuerySelector("input")?.GetAttribute("value") == name)
           ?? throw new InvalidOperationException($"No {kind} rule '{name}'.");

    public IElement LastRuleRow(string kind) => Section(RulesSection(kind)).QuerySelectorAll("tbody tr").Last();

    public IElement AuthHelper => Stubs.Find(".auth-helper");

    public IElement AuthHelperField(string label)
        => AuthHelper.QuerySelectorAll("label").First(l => OwnText(l) == label).QuerySelector("input")!;
}
