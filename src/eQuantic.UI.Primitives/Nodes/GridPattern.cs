namespace eQuantic.UI.Primitives;

/// <summary>
/// A repeating hairline GRID — the "graph paper" backdrop marketing surfaces use behind a hero.
/// Deliberately a closed shape (square cell, 1dp lines, one color), not a general pattern/texture
/// API: that keeps it realizable on BOTH targets with primitives that already exist. Web lowers to
/// the two repeating <c>linear-gradient</c> layers with <c>background-size</c>; native emits the
/// hairlines as ordinary fills bounded by the box — no engine primitive, no shader.
/// </summary>
/// <param name="Cell">Grid spacing in dp (the design's hero uses 56).</param>
/// <param name="Color">Line color — a token, so the grid tracks light/dark like everything else.</param>
/// <param name="LineWidth">Line thickness in dp. 1 is the only value the design uses.</param>
public readonly record struct GridPattern(float Cell, ColorToken Color, float LineWidth = 1);
