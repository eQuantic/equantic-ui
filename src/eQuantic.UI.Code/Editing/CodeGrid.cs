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
/// </summary>
public readonly record struct CodeGrid(Point Origin, Size Cell)
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

    /// <summary>Where a caret before <paramref name="column"/> on <paramref name="line"/> sits.</summary>
    public Point PointOf(int line, int column) =>
        new(Origin.X + column * Cell.Width, Origin.Y + line * Cell.Height);
}
