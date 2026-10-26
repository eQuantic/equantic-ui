using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// The viewport a sheet surface draws its grid in, as the engine reads a point against it: the scroll
/// that places the cells (read LIVE, because a scroll step inside the cells already built builds
/// nothing and hands the engine nothing new) and the pinned bands in front of the cells, the column
/// headers' height and the row numbers' width. With the document's spans these place every cell the
/// surface shows, which is the whole reason they live in the engine and in no host: two hosts doing
/// the same arithmetic is how a click lands a cell away from the band the component drew.
/// <para>
/// The component that composes the viewport hands it over as it builds, before it reads the
/// selection, as a code editor hands its grid to its engine, with the cells' type: the cell's editor is
/// placed in it.
/// </para>
/// </summary>
public readonly record struct SheetGrid(ITableScrollModel? Scroll, float HeaderWidth, float HeaderHeight)
{
    /// <summary>
    /// What a surface nobody has placed yet draws: no headers, no scroll, the sheet's first cell at the
    /// surface's corner. A property that builds on READ, not a field built when the type loads: in the
    /// browser the engine is a module in a barrel, and a static initializer that constructs another
    /// module's class runs before that class exists whenever the barrel's order puts this module first.
    /// </summary>
    public static SheetGrid Default => new(null, 0, 0);

    /// <summary>Where the grid stands under the window: none when nothing scrolls it.</summary>
    public TableOffset Offset => Scroll?.Offset ?? TableOffset.Zero;

    /// <summary>
    /// How wide a run of text is in the cells' own type: what places the entry's caret, its selection and a
    /// press inside the cell's editor, and what the editor grows by. Null where nothing measures (a render
    /// with no measurer), and the editor then places text on a monospaced pitch.
    /// </summary>
    public Func<string, float>? Measure { get; init; }

    /// <summary>The height of a line of the cells' type.</summary>
    public float LineHeight { get; init; }

    /// <summary>What a cell keeps between its gridlines and its text, on each side.</summary>
    public EdgeInsets CellPadding { get; init; }

    /// <summary>How wide the resize grip on a header's trailing edge is: a press there is the grip's, which
    /// resizes the column or the row, and selects nothing. 0 where the headers draw no grip.</summary>
    public float ResizeGrip { get; init; }
}
