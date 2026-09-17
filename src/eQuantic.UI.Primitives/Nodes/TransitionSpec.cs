namespace eQuantic.UI.Primitives;

/// <summary>
/// Animates CHANGES to a box's style (spec S6): whenever a covered channel's value swaps — a hover
/// diff engaging, the scrolled variant kicking in, a re-render landing a different transform or
/// max-width — the change GLIDES over <see cref="DurationMs"/> along <see cref="Easing"/> instead
/// of snapping. Web is CSS transitions exactly; Photon fence: the style interpolator (until it
/// lands, native SNAPS — honesty over smoothness, the same fence Flexible.AnimateChanges documents).
/// </summary>
public readonly record struct TransitionSpec(StyleChannels Channels, float DurationMs = Motion.BaseMs, float DelayMs = 0)
{
    /// <summary>The bezier the change follows; defaults to the spec §06 standard curve.</summary>
    public Curve Easing { get; init; } = Curve.Standard;

    /// <summary>
    /// A NAMED motion role applied to channels — <c>TransitionSpec.Of(StyleChannels.Colors,
    /// Motion.Press)</c>. Prefer this to a hand-picked number: the role carries both the duration
    /// AND the curve the spec pairs with it, so authors never land off the 100/200/300 scale.
    /// </summary>
    public static TransitionSpec Of(StyleChannels channels, MotionSpec motion) =>
        new(channels, motion.DurationMs) { Easing = motion.Curve };

    /// <summary>Color-only glide — the ubiquitous hover-tint transition (spec §06: fast feedback).</summary>
    public static TransitionSpec Colors(float durationMs = Motion.FastMs) => new(StyleChannels.Colors, durationMs);

    /// <summary>Everything glides — state swaps that recolor, move and re-shadow at once.</summary>
    public static TransitionSpec All(float durationMs = Motion.BaseMs) => new(StyleChannels.All, durationMs);
}
