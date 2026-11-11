namespace eQuantic.UI.Primitives;

/// <summary>
/// The table family's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): Table and DataTable, every member
/// OPTIONAL. A member left null is the component's own default. The fill a selected row takes from
/// <see cref="IAppTheme.Colors"/> stays there.
/// <para>
/// The two share a cell's padding and the hover fill and differ in the rest: a Table's rows are at least
/// 44 tall and separated by dividers, a DataTable's are 52 tall and bordered, with a filled header in
/// capitals.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> <c>DataTableThemeData</c> on <c>ThemeData</c>: the row heights,
/// the paddings, the header's text and colours, nullable.
/// </para>
/// </summary>
public sealed record TableTheme
{
    /// <summary>A cell's horizontal padding (12).</summary>
    public float? CellPaddingX { get; init; }

    /// <summary>A cell's vertical padding (8).</summary>
    public float? CellPaddingY { get; init; }

    /// <summary>The fill a hovered row takes (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? HoverFill { get; init; }

    // ---- Table ------------------------------------------------------------------------------------

    /// <summary>The gap between a Table's columns (12).</summary>
    public float? ColumnGap { get; init; }

    /// <summary>A Table row's minimum height (44).</summary>
    public float? RowMinHeight { get; init; }

    /// <summary>A Table header's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? HeaderColor { get; init; }

    // ---- DataTable --------------------------------------------------------------------------------

    /// <summary>A DataTable row's height, its loading rows' included (52).</summary>
    public float? DataRowHeight { get; init; }

    /// <summary>The width of a selectable DataTable's leading column (44).</summary>
    public float? SelectionColumnWidth { get; init; }

    /// <summary>The fill of a DataTable's header (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? HeaderFill { get; init; }

    /// <summary>A DataTable header's colour when its column is not sorted
    /// (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? DataHeaderColor { get; init; }

    /// <summary>A DataTable header's colour when its column is sorted, and its chevron's
    /// (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? SortedHeaderColor { get; init; }

    /// <summary>Whether a DataTable header is set in capitals (true).</summary>
    public bool? HeaderUppercase { get; init; }

    /// <summary>The gap between a DataTable header and its chevron (4).</summary>
    public float? HeaderIconGap { get; init; }

    /// <summary>The sort chevron's size (16), a size on the §07 whitelist (16, 20, 24 or 32): an icon of
    /// any other size throws when it is built.</summary>
    public float? SortIconSize { get; init; }

    /// <summary>The width of a DataTable's header and row borders (1).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>The colour of a DataTable's header and row borders (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? BorderColor { get; init; }
}
