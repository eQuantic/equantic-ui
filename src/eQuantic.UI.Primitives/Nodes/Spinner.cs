namespace eQuantic.UI.Primitives;

/// <summary>
/// The Spinner node (spec B15) — the canonical activity indicator on BOTH platforms: 8 rrect bars
/// (2×5 in the 16dp em-box, scaled by <see cref="Size"/>), opacity phase-staggered on an 800ms/rev
/// linear clock. Deliberately NO arcs — the spec draws it inside the v1 engine fence (rrects +
/// opacity only). Sizes come from the §07 icon whitelist; color inherits the context text token
/// (web <c>currentColor</c>; native falls back to TextPrimary like Icon). Reduce Motion: the bars
/// pulse in place — same fade, no rotation phase (spec B15). Behavior fences: the 400ms
/// anti-flash appear delay is generated CSS on web and joins the native transition animator;
/// context labels and busy announcements ride the a11y system.
/// </summary>
public sealed class Spinner : VisualNode
{
    /// <summary>Spec B15 clock: one revolution across the 8 bars every 800ms, linear.</summary>
    public const int RevolutionMs = 800;

    /// <summary>Spec B15 anti-flash: the spinner appears only after 400ms (skip flash for fast ops).</summary>
    public const int AppearDelayMs = 400;

    public override string NodeKind => "spinner";

    public Spinner(float size = IconSize.Dense, ColorToken? color = null)
    {
        if (size is not (16 or 20 or 24 or 32))
            throw new ArgumentOutOfRangeException(nameof(size),
                $"Spinner size {size} is not on the §07 whitelist (16/20/24/32 — IconSize.Sm/Dense/Md/Lg).");
        Size = size;
        Color = color;
    }

    public float Size { get; }

    /// <summary>Tint token; null inherits the context text color (like Icon/Text).</summary>
    public ColorToken? Color { get; init; }
}
