namespace eQuantic.UI.Sheet;

/// <summary>
/// Where a sheet's selection stands: every range selected, the active one last, and the active cell inside
/// it, as Excel's <c>Selection.Areas</c> and <c>ActiveCell</c> describe it. An edit records one before and
/// one after it, so an undo or a redo of a step taken over several ranges brings all of them back (#1100).
/// </summary>
/// <param name="Ranges">Every range, the active one last; never empty.</param>
/// <param name="Active">The active cell, inside the active range.</param>
public sealed record SheetSelection(IReadOnlyList<SheetRange> Ranges, CellRef Active)
{
    /// <summary>One range, the cell at its anchor active: what a sheet starts with, and what one press leaves.</summary>
    public SheetSelection(SheetRange range) : this([range], range.Anchor) { }

    /// <summary>The active range: the last one.</summary>
    public SheetRange Range => Ranges[Ranges.Count - 1];
}
