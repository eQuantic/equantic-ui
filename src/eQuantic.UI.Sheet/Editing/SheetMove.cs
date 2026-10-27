namespace eQuantic.UI.Sheet;

/// <summary>
/// What the last change of the selection moved (<see cref="SheetController.Moved"/>, #1081): what a view
/// that announces the grid's moves says next, the active cell or the active range.
/// </summary>
public enum SheetMove
{
    /// <summary>The active cell moved: to a single cell, or within a range that stayed.</summary>
    ActiveCell,

    /// <summary>The active range changed and covers more than one cell: Shift stretched it, a drag drew it,
    /// a header selected a band.</summary>
    ActiveRange,
}
