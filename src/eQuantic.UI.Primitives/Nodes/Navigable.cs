namespace eQuantic.UI.Primitives;

/// <summary>
/// A TWO-DIMENSIONAL composite: one Tab stop for the whole thing, and a keyboard that moves a
/// selection around inside it. The 2-D twin of <see cref="Adjustable"/>, and the reason it is a
/// separate node rather than a flag on it: an Adjustable answers ±1 along ONE axis, while a grid
/// answers rows, pages, sections and row bounds (see <see cref="NavigableMove"/>).
/// <para>
/// It is FOCUS-SCOPED by construction — the handler lives on the host element, so two calendars on
/// one page never answer the same arrow press. That is the distinction from <see cref="Shortcut"/>,
/// which is page-level by design (mounting is the subscription) and stays the right tool for
/// Esc-dismiss and ⌘K. A composite's own navigation is not a page shortcut, and treating it as one
/// is how an inline calendar and the list beneath it end up fighting over ArrowDown.
/// </para>
/// <para>
/// Structure, not just keys: a <c>grid</c> whose cells are not inside <c>row</c>s is an invalid
/// accessibility tree, so the rows are declared here (<see cref="Rows"/>) and the realizer wraps
/// each one in a row that is TRANSPARENT to layout — the caller keeps every layout and styling
/// decision it already had. <see cref="HasHeaderRow"/> marks the first row as the column headers
/// (the day-name row of C15).
/// </para>
/// </summary>
public sealed class Navigable : VisualNode
{
    public override string NodeKind => "navigable";

    public Navigable(IReadOnlyList<VisualNode> rows, Action<NavigableMove> onMove)
    {
        Rows = rows;
        OnMove = onMove;
    }

    /// <summary>The rows, in reading order. Each becomes one row of the accessibility tree; how it
    /// LOOKS is the caller's — a Row node, a slice of a Grid, whatever the design asks for.</summary>
    public IReadOnlyList<VisualNode> Rows { get; init; }

    /// <summary>Where the keyboard asked to go. The COMPOSITE owns what a move means — which cell
    /// is focused, whether the ends wrap, whether a page is a month or a screenful — exactly as
    /// <see cref="Adjustable.OnAdjust"/> leaves the step's worth to its control.</summary>
    public Action<NavigableMove> OnMove { get; init; }

    /// <summary>Announced by assistive tech as the composite's name ("July 2026").</summary>
    public string Label { get; init; } = "";

    public NavigableRole Role { get; init; } = NavigableRole.Grid;

    /// <summary>Whether <see cref="Rows"/>[0] holds the COLUMN HEADERS rather than cells — the
    /// day-name row a calendar puts above the days.</summary>
    public bool HasHeaderRow { get; init; }

    /// <summary>The cell that currently holds the composite's focus, as its path within the grid
    /// (row, item). Null while nothing is focused. The web twin points
    /// <c>aria-activedescendant</c> at it, so a screen reader announces the cell the arrows moved
    /// to WITHOUT the focus ever leaving the composite's one stop.</summary>
    public (int Row, int Item)? ActiveCell { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
