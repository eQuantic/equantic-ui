namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="TableViewCell"/> is in a table or a grid (#1081), as Flutter's <c>SemanticsRole</c>
/// says <c>cell</c> and <c>columnHeader</c>.
/// </summary>
public enum TableCellRole
{
    /// <summary>A cell, named by what it holds.</summary>
    Cell,

    /// <summary>The header of its column, which names the cells beneath it.</summary>
    ColumnHeader,

    /// <summary>The header of its row, which names the cells beside it.</summary>
    RowHeader,

    /// <summary>Nothing a reader reaches: the corner between a table's two headers.</summary>
    None,
}
