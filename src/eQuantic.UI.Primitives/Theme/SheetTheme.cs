namespace eQuantic.UI.Primitives;

/// <summary>
/// The spreadsheet's slot of the theme (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): every colour the grid
/// draws that a brand may want its own, each OPTIONAL. A member left out is the spreadsheet's default,
/// derived from the theme's foundation colours, so a theme that never thought about sheets draws one
/// correctly and one that names a single gridline changes that alone.
/// <para>
/// <em>How does Flutter solve it?</em> <c>DataTableThemeData</c> on <c>ThemeData</c>: every member
/// nullable, the widget defaulting from the colour scheme. Members are data, never a delegate, because
/// the slot crosses to the browser in the theme's JSON.
/// </para>
/// </summary>
public sealed record SheetTheme
{
    /// <summary>The lines between cells, and between headers.</summary>
    public ColorToken? Gridline { get; init; }

    /// <summary>The column headers' and the row numbers' background.</summary>
    public ColorToken? HeaderBackground { get; init; }

    /// <summary>The column headers' and the row numbers' text.</summary>
    public ColorToken? HeaderText { get; init; }

    /// <summary>A header's background where the selection's band crosses it.</summary>
    public ColorToken? HeaderSelectedBackground { get; init; }

    /// <summary>A header's text where the selection's band crosses it.</summary>
    public ColorToken? HeaderSelectedText { get; init; }

    /// <summary>A cell's background.</summary>
    public ColorToken? CellBackground { get; init; }

    /// <summary>A cell's text.</summary>
    public ColorToken? CellText { get; init; }

    /// <summary>The fill over a range of more than one cell.</summary>
    public ColorToken? RangeFill { get; init; }

    /// <summary>The outline of the active cell.</summary>
    public ColorToken? ActiveOutline { get; init; }

    /// <summary>The outline of a fill's preview.</summary>
    public ColorToken? FillPreview { get; init; }

    /// <summary>The fill handle, the square on the selection's corner.</summary>
    public ColorToken? FillHandle { get; init; }

    /// <summary>The band under the text selected inside an entry: in the cell's editor, the formula bar
    /// and the name box.</summary>
    public ColorToken? EntrySelection { get; init; }
}
