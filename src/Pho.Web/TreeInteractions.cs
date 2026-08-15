using Pho.Domain;

namespace Pho.Web;

/// <summary>
/// A row activation from the tree: the row, plus the modifier keys that decide whether it
/// replaces, toggles, or extends the selection (ctrl/cmd-click and shift-click).
/// </summary>
public sealed record RowActivation(TreeRow Row, bool Ctrl = false, bool Shift = false);

/// <summary>
/// A completed drag: the row that was dragged, and the row it was dropped on — null meaning
/// the tree root. Whether the whole selection moves is decided by the page.
/// </summary>
public sealed record RowDrop(TreeRow Dragged, TreeRow? Target);

/// <summary>A group as offered in a picker, indented to show where it sits in the tree.</summary>
public sealed record GroupOption(Guid Id, string Label);
