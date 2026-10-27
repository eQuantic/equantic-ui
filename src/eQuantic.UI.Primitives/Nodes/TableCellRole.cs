namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="TableViewCell"/> is in a table or a grid (#1081), as Flutter's <c>SemanticsRole</c>
/// says <c>cell</c> and <c>columnHeader</c>.
/// <para>
/// There is no cell a reader does not reach, the corner between two headers included, which is the empty
/// header of the row headers' column. A browser places a cell by its order among its row's cells, not by
/// the column it names: with the corner left out of its row, Safari read every column's header over the
/// column before it.
/// </para>
/// </summary>
public enum TableCellRole
{
    /// <summary>A cell, named by what it holds.</summary>
    Cell,

    /// <summary>The header of its column, which names the cells beneath it.</summary>
    ColumnHeader,

    /// <summary>The header of its row, which names the cells beside it.</summary>
    RowHeader,
}
