namespace eQuantic.UI.Primitives;

/// <summary>
/// A cell address: 0-based row and column. <see cref="Key"/> is the SINGLE integer the document
/// indexes by — a record struct makes a fine C# dictionary key and a useless JavaScript Map key
/// (identity, not value), so the key crosses targets as an int. 16384 columns is Excel's own
/// ceiling, which makes the packing stable and the interop honest.
/// </summary>
public readonly record struct CellRef(int Row, int Col) : IComparable<CellRef>
{
    public const int MaxCols = 16384;

    public int Key => Row * MaxCols + Col;

    public static CellRef FromKey(int key) => new(key / MaxCols, key % MaxCols);

    public int CompareTo(CellRef other) =>
        Row != other.Row ? Row.CompareTo(other.Row) : Col.CompareTo(other.Col);

    /// <summary>"A1" for (0,0) — the spelling every spreadsheet user already reads.</summary>
    public string ToA1()
    {
        var name = "";
        var col = Col;
        while (true)
        {
            name = (char)('A' + col % 26) + name;
            col = col / 26 - 1;
            if (col < 0) break;
        }
        return name + (Row + 1);
    }

    /// <summary>Parses "B12" back to (11, 1). Null for anything that is not an A1 address.</summary>
    public static CellRef? ParseA1(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var col = 0;
        var i = 0;
        while (i < text.Length && text[i] >= 'A' && text[i] <= 'Z')
        {
            col = col * 26 + (text[i] - 'A' + 1);
            i++;
        }
        if (i == 0 || i == text.Length) return null;
        var row = 0;
        while (i < text.Length)
        {
            if (text[i] < '0' || text[i] > '9') return null;
            row = row * 10 + (text[i] - '0');
            i++;
        }
        if (row == 0) return null;
        return new CellRef(row - 1, col - 1);
    }
}

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
