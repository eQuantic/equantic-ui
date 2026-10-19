namespace eQuantic.UI.Primitives;

/// <summary>
/// A rectangle of cells in a table: the row and the column it starts at, and how many rows and
/// columns it covers.
/// </summary>
public readonly record struct TableRange(int Row, int Column, int Rows = 1, int Columns = 1)
{
    /// <summary>The last row it covers.</summary>
    public int LastRow => Row + Rows - 1;

    /// <summary>The last column it covers.</summary>
    public int LastColumn => Column + Columns - 1;
}
