using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// The grid the code is drawn on, in the surface's own coordinates: where line 0, column 0 begins
/// (<see cref="Origin"/>) and the size of one character cell (<see cref="Cell"/> — a column wide, a
/// line tall). With a fixed pitch these two place every caret, every selection band and every click,
/// which is the whole reason they live in the engine and in no host: two hosts computing them
/// separately is how a caret ends up a character away from where a click put it.
/// <para>
/// A point and a size rather than four floats, because the vocabulary has a word for each and a
/// grid is neither a box nor a set of insets — it is an origin and a pitch, repeated.
/// </para>
/// <para>
/// With <see cref="Rows"/> a line is placed on its ROW, which fillers and folded runs set apart
/// from its index (docs/CODE-EDITOR-PLAN.md, the shape, §9): a diff's padding, a collapsed run of
/// unchanged lines. Without it a row is a line.
/// </para>
/// </summary>
public readonly record struct CodeGrid(Point Origin, Size Cell, CodeRows? Rows = null)
{
    /// <summary>
    /// What a surface nobody has measured yet draws on: 8dp columns, 18dp lines, the content at the
    /// surface's corner.
    /// <para>
    /// A property that builds on READ, not a field built when the type loads: on the web the engine
    /// is a module in a barrel, and a static initializer that constructs another module's class runs
    /// at evaluation time — before that class exists, whenever the barrel's order puts this module
    /// first. The runtime has been bitten by exactly that twice.
    /// </para>
    /// </summary>
    public static CodeGrid Default => new(Point.Zero, new Size(8, 18));

    /// <summary>The row <paramref name="line"/> is drawn on.</summary>
    public int RowOf(int line) => Rows?.RowOf(line) ?? line;

    /// <summary>Where a caret before <paramref name="column"/> on <paramref name="line"/> sits.</summary>
    public Point PointOf(int line, int column) =>
        new(Origin.X + column * Cell.Width, Origin.Y + RowOf(line) * Cell.Height);

    /// <summary>
    /// The line a point <paramref name="y"/> down the surface lands on: the line of its row, the line
    /// a filler stands before, or the first line a placeholder hides. Not clamped to the document,
    /// which the caller knows and the grid does not.
    /// </summary>
    public int LineAt(float y)
    {
        var row = (int)MathF.Floor((y - Origin.Y) / Cell.Height);
        return Rows is { } rows ? rows.LineAtRow(Math.Max(0, row)) : row;
    }
}
