namespace eQuantic.UI.Sheet;

/// <summary>
/// A rectangular selection: the ANCHOR is where it started, the FOCUS is the corner the keyboard
/// moves — the same two-ended shape the code editor's range has, in two dimensions. A single cell
/// is a range whose ends agree.
/// </summary>
public readonly record struct SheetRange(CellRef Anchor, CellRef Focus)
{
    public SheetRange(CellRef cell) : this(cell, cell) { }

    public bool IsSingleCell => Anchor == Focus;

    public int TopRow => Math.Min(Anchor.Row, Focus.Row);
    public int BottomRow => Math.Max(Anchor.Row, Focus.Row);
    public int LeftCol => Math.Min(Anchor.Col, Focus.Col);
    public int RightCol => Math.Max(Anchor.Col, Focus.Col);

    public int RowCount => BottomRow - TopRow + 1;
    public int ColCount => RightCol - LeftCol + 1;

    public bool Contains(CellRef cell) =>
        cell.Row >= TopRow && cell.Row <= BottomRow && cell.Col >= LeftCol && cell.Col <= RightCol;

    /// <summary>The top-left corner — where a paste lands, where reading starts.</summary>
    public CellRef TopLeft => new(TopRow, LeftCol);
}
