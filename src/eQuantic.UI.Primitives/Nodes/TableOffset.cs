namespace eQuantic.UI.Primitives;

/// <summary>
/// How far a <see cref="TableViewport"/> has scrolled its grid, across and down, in points. Two
/// <see langword="double"/>s rather than a <see cref="Point"/>: the bottom of Excel's grid is 29 million
/// points down, where a <see langword="float"/> no longer holds a point exactly.
/// </summary>
[ZeroConstructs("value-types.ts: `constructor(across = 0, down = 0)`.")]
public readonly record struct TableOffset(double Across, double Down)
{
    /// <summary>The grid's first row and first column at the window's corner.</summary>
    public static TableOffset Zero => new(0, 0);
}
