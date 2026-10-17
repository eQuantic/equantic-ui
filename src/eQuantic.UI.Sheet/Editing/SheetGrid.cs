using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// The window a sheet surface draws, in the surface's own coordinates: where the window's first cell
/// begins (<see cref="Origin"/>) and which cell that is (<see cref="FirstRow"/>, <see cref="FirstCol"/>).
/// With the document's row heights and column widths these place every cell the surface shows, which
/// is the whole reason they live in the engine and in no host: two hosts doing the same prefix sums
/// is how a click lands a cell away from the band the component drew.
/// <para>
/// The component that draws the window hands it over as it builds, before it reads the selection,
/// as a code editor hands its grid to its engine: a virtualized sheet's first row moves with every
/// scroll, and a child that draws its own headers places its first cell past them.
/// </para>
/// </summary>
public readonly record struct SheetGrid(Point Origin, int FirstRow, int FirstCol)
{
    /// <summary>
    /// What a surface nobody has placed yet draws: the sheet's first cell at the surface's corner.
    /// A property that builds on READ, not a field built when the type loads: in the browser the
    /// engine is a module in a barrel, and a static initializer that constructs another module's
    /// class runs before that class exists whenever the barrel's order puts this module first.
    /// </summary>
    public static SheetGrid Default => new(Point.Zero, 0, 0);
}
