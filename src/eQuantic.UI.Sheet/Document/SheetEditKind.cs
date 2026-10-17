namespace eQuantic.UI.Sheet;

/// <summary>What kind of change a <see cref="SheetEdit"/> records.</summary>
public enum SheetEditKind : byte
{
    SetCells = 0,
    InsertRows = 1,
    DeleteRows = 2,
    InsertCols = 3,
    DeleteCols = 4,
    ResizeRow = 5,
    ResizeCol = 6,
}
