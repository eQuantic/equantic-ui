namespace eQuantic.UI.Primitives;

/// <summary>
/// A partial style applied OVER the base while an interaction state is active (spec S5) — the
/// declarative twin of CSS pseudo-classes: no event handlers in app code, each realizer implements
/// the state natively (pseudo-class rules on web — zero JS; the interaction system on Photon).
/// Only the set members override; everything else keeps the base value.
/// </summary>
public readonly record struct StyleDiff
{
    public ColorToken? Background { get; init; }
    public ColorToken? BorderColor { get; init; }
    /// <summary><c>null</c> = keep the base border width.</summary>
    public float? BorderWidth { get; init; }
    /// <summary><c>null</c> = keep the base elevation.</summary>
    public int? Elevation { get; init; }
    /// <summary><c>null</c> = keep the base opacity.</summary>
    public float? Opacity { get; init; }
    /// <summary>Swaps the gradient fill while active (the design's gradient-button hover).
    /// <c>null</c> = keep the base gradient.</summary>
    public LinearGradient? Gradient { get; init; }

    /// <summary>Backdrop blur radius while active (the scrolled header's frosted veil).
    /// <c>null</c> = keep the base.</summary>
    public float? BackdropBlur { get; init; }

    /// <summary>
    /// The transform while active, REPLACING the base's <see cref="BoxStyle.Transform"/> the way a
    /// CSS rule replaces <c>transform</c>: a box rotated at rest and lifted on hover says both here.
    /// Center-anchored and paint-only, like the base's. <c>null</c> = keep the base (#504).
    /// </summary>
    public Transform2D? Transform { get; init; }

    /// <summary>
    /// The custom shadows while active, REPLACING the base's <see cref="BoxStyle.Shadow"/> and
    /// <see cref="BoxStyle.Shadows"/>: a glow that deepens under the pointer. The elevation's shadow
    /// and the inset highlight stay in the list either way, in the base's order. <c>null</c> = keep
    /// the base (#504).
    /// </summary>
    public IReadOnlyList<ShadowSpec>? Shadows { get; init; }

    public bool IsEmpty =>
        Background is null && BorderColor is null && BorderWidth is null
        && Elevation is null && Opacity is null && Gradient is null && BackdropBlur is null
        && Transform is null && Shadows is null;
}
