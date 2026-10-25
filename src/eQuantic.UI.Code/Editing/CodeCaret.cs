namespace eQuantic.UI.Code;

/// <summary>
/// One caret of several (#1012): its selection, and the cell a run of ↑ and ↓ aims at, which each caret
/// keeps on its own, -1 when it aims at none. Moving down through a short line and on to a long one brings
/// every caret back to the column it started from, as one caret's run does.
/// </summary>
internal readonly record struct CodeCaret(CodeRange Selection, int Cell);
