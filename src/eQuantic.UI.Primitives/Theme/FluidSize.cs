namespace eQuantic.UI.Primitives;

/// <summary>
/// A size that follows the window between a floor and a ceiling: <see cref="PercentOfWindow"/>
/// percent of the window's width, never under <see cref="Min"/> nor over <see cref="Max"/>, in dp.
/// What a handoff writes as <c>clamp(34px, 4.2vw, 54px)</c>, and what one number per window class
/// can only sample: the heading grows with the window instead of jumping at each class boundary.
/// </summary>
/// <param name="Min">The floor, in dp.</param>
/// <param name="PercentOfWindow">The share of the window's width, in percent (CSS's <c>vw</c>).</param>
/// <param name="Max">The ceiling, in dp.</param>
public readonly record struct FluidSize(float Min, float PercentOfWindow, float Max)
{
    /// <summary>The size at a window <paramref name="windowWidth"/> dp wide.</summary>
    public float At(float windowWidth) => Math.Clamp(windowWidth * PercentOfWindow / 100f, Min, Max);
}
