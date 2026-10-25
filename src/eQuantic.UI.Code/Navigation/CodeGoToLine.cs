namespace eQuantic.UI.Code;

/// <summary>
/// The line the go-to field reads (#1083), as VS Code's "Go to Line" reads it: <see cref="Line"/> counted
/// from 1 and clamped to the document, and the <see cref="Column"/> typed after it, a VISIBLE column counted
/// from 1, where a tab takes the cells to its stop, clamped to the line's <see cref="Columns"/>. Null when
/// none was typed, and then <see cref="ColumnStarted"/> says whether the separator before one was.
/// </summary>
public sealed record CodeGoToLine(int Line, int? Column, bool ColumnStarted, int Columns);
