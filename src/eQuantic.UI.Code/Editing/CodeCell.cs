namespace eQuantic.UI.Code;

/// <summary>
/// One text element of a line on the grid (see <see cref="CodeLineCells"/>): the columns it spans in
/// the document, from <see cref="Start"/> up to <see cref="End"/>, and the cells it takes on screen,
/// <see cref="Width"/> of them from <see cref="Cell"/>.
/// </summary>
public readonly record struct CodeCell(int Start, int End, int Cell, int Width);
