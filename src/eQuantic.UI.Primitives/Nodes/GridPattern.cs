namespace eQuantic.UI.Primitives;

/// <summary>
/// A repeating hairline GRID: the "graph paper" backdrop marketing surfaces use behind a hero. Square
/// cells, one colour, rules that start at the placing area's origin (<see cref="Pattern.Area"/>) and
/// continue across the clip area on every side. The web writes it as two repeating
/// <c>linear-gradient</c> layers; Photon draws the rules as fills inside the clip area's rounded
/// rectangle.
/// </summary>
/// <param name="Cell">Grid spacing in dp (the design's hero uses 56).</param>
/// <param name="Color">Line color — a token, so the grid tracks light/dark like everything else.</param>
/// <param name="LineWidth">Line thickness in dp. 1 is the only value the design uses.</param>
public sealed record GridPattern(float Cell, ColorToken Color, float LineWidth = 1) : Pattern
{
    private protected override TResult Dispatch<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state) =>
        visitor.Visit(this, state);
}
