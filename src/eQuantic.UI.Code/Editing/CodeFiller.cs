namespace eQuantic.UI.Code;

/// <summary>
/// <see cref="Rows"/> rows that belong to no line of the document, drawn before line
/// <see cref="BeforeLine"/> (the line count to draw them after the last line). Empty rows are the
/// padding that keeps two sides of a diff level. With a <see cref="SourceLine"/> they show lines of
/// ANOTHER document from that line on: the lines a change removed, in an inline diff, drawn between
/// the lines of the text that replaced them.
/// </summary>
public readonly record struct CodeFiller(int BeforeLine, int Rows, int SourceLine = -1);
