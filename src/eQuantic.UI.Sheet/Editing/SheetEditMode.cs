namespace eQuantic.UI.Sheet;

/// <summary>
/// What the arrows do while a cell is being edited, Excel's two modes. An entry begun by typing is in
/// <see cref="Enter"/> mode, where an arrow commits it and moves the active cell, which is how a column
/// is typed down; an entry begun with F2, a double click or a press in the formula bar is in
/// <see cref="Edit"/> mode, where the arrows move the caret through the text. F2 switches between them.
/// </summary>
public enum SheetEditMode
{
    /// <summary>An arrow commits the entry and moves the active cell.</summary>
    Enter,

    /// <summary>An arrow moves the entry's caret.</summary>
    Edit,
}
