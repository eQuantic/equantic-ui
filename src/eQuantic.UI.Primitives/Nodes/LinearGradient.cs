namespace eQuantic.UI.Primitives;

/// <summary>
/// The engine fence's gradient, exactly: TWO color stops on a straight axis — plus an optional
/// <see cref="Via"/> midpoint (the design system's from/via/to triple; brand text and glossy fills
/// need the hue turn). Stops are TOKENS (mode-free trees); realizers resolve per mode — web as
/// <c>linear-gradient(to right|bottom, from, via P%, to)</c>, native as <c>Paint.Linear</c> across
/// the box bounds. Photon fence: the shader interpolates TWO stops — until the 3-stop paint lands,
/// native paints <see cref="From"/>→<see cref="To"/> and the midpoint is web-only (annotated,
/// honesty over silence).
/// </summary>
public readonly record struct LinearGradient(
    ColorToken From,
    ColorToken To,
    GradientDirection Direction = GradientDirection.ToRight)
{
    /// <summary>Optional middle stop at <see cref="ViaPosition"/>. <c>null</c> = plain 2-stop.</summary>
    public ColorToken? Via { get; init; }

    /// <summary>Fraction of the axis where <see cref="Via"/> sits (0–1); the design's triples
    /// pivot at the middle.</summary>
    public float ViaPosition { get; init; } = 0.5f;
}
