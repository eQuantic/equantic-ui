namespace eQuantic.UI.Sheet;

/// <summary>How far one selection movement goes.</summary>
public enum SheetMotion : byte
{
    Cell = 0,
    /// <summary>Excel's Ctrl+arrow: to the edge of the data block in that direction.</summary>
    DataEdge = 1,
    /// <summary>Home/End on the row; Ctrl+Home/End to the sheet's corners.</summary>
    RowBoundary = 2,
    SheetBoundary = 3,
}
