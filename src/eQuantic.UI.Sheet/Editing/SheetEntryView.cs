namespace eQuantic.UI.Sheet;

/// <summary>
/// Which view of the one entry holds the caret: the cell's own editor or the formula bar. Both show the
/// same text after every keystroke, because both read the same document; only the one that holds the
/// caret draws it, its selection and its composition's place, as Excel's inactive view shows text alone.
/// </summary>
public enum SheetEntryView
{
    /// <summary>The editor drawn over the active cell.</summary>
    Cell,

    /// <summary>The formula bar the app placed beside the grid.</summary>
    FormulaBar,
}
