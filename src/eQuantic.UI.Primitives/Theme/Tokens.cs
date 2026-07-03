
namespace eQuantic.UI.Primitives;

/// <summary>
/// Spacing scale (spec §03) — 4dp base, gap-owned layout (Photon has NO margin property: parents own
/// spacing via Gap/Padding). Components may not hardcode dp values off this scale.
/// </summary>
public static class Space
{
    public const float S1 = 4;    // icon↔text micro-gap, badge padding
    public const float S2 = 8;    // gap inside controls, chip gaps
    public const float S3 = 12;   // compact control padding, list gaps
    public const float S4 = 16;   // screen gutter, card padding, list inset
    public const float S5 = 20;   // roomy card padding, sheet gutter
    public const float S6 = 24;   // between content groups
    public const float S8 = 32;   // section breaks
    public const float S10 = 40;  // hero padding
    public const float S12 = 48;  // empty-state breathing room
    public const float S16 = 64;  // large vertical rhythm
}

/// <summary>
/// Radius scale (spec §04). <see cref="Full"/> relies on the engine's CSS-rule clamp
/// (<c>RRect.Normalized()</c> caps radii at min(w,h)/2), so it is safe on any shape.
/// </summary>
public static class Radius
{
    public const float Xs = 4;    // badges, checkbox
    public const float Sm = 6;    // chips, small controls
    public const float Md = 10;   // buttons, inputs
    public const float Lg = 14;   // cards, banners
    public const float Xl = 20;   // sheets, dialogs
    public const float Full = 999; // pills, avatars, FAB (engine-clamped)
}

/// <summary>Canonical icon sizes (spec §07) — glyphs via the text atlas; icons do NOT scale with Dynamic Type.</summary>
public static class IconSize
{
    public const float Sm = 16;   // inline
    public const float Dense = 20; // dense UI
    public const float Md = 24;   // default
    public const float Lg = 32;   // feature
}

/// <summary>Touch-target rules (spec §08): the engine enforces the stricter 48dp on both platforms.</summary>
public static class Touch
{
    /// <summary>Minimum hit-rect side; visuals may be smaller — the framework expands hit-slop symmetrically.</summary>
    public const float MinTarget = 48;

    /// <summary>Drag beyond this distance from the hit rect cancels a press without firing.</summary>
    public const float PressCancelSlop = 12;
}

/// <summary>
/// One analytic rrect shadow (spec §05): <c>ShadowSpec(OffsetY, Blur, Spread, Color)</c>. Exactly one
/// shadow per node — stacked shadows are a spec violation. Level 0 is "none + border".
/// </summary>
public readonly record struct ShadowSpec(float OffsetY, float Blur, float Spread, ColorToken Color)
{
    public bool IsNone => Blur == 0 && OffsetY == 0 && Spread == 0;
}

/// <summary>Easing curves (spec §06). Cubic-bézier control points, plus the physical spring.</summary>
public readonly record struct Curve(float X1, float Y1, float X2, float Y2)
{
    /// <summary>On-screen moves: tab indicator, segmented thumb, reorder.</summary>
    public static readonly Curve Standard = new(0.2f, 0f, 0f, 1f);
    /// <summary>Entrances: sheets up, toasts in, page push.</summary>
    public static readonly Curve Decelerate = new(0f, 0f, 0f, 1f);
    /// <summary>Exits (paired with ⅔ of the enter duration).</summary>
    public static readonly Curve Accelerate = new(0.3f, 0f, 1f, 1f);
}

/// <summary>Spring parameters for gesture releases (sheet snap, swipe settle) — physical, never keyframed;
/// initial velocity is injected from the gesture velocity at release.</summary>
public readonly record struct SpringSpec(float Stiffness, float Damping, float Mass)
{
    public static readonly SpringSpec Default = new(380, 34, 1);
}

/// <summary>Motion durations (spec §06) — animate transform &amp; opacity only.</summary>
public static class Motion
{
    /// <summary>Pressed feedback and hover-like states.</summary>
    public const int FastMs = 100;
    /// <summary>Most state changes.</summary>
    public const int BaseMs = 200;
    /// <summary>Surface enter/exit.</summary>
    public const int SlowMs = 300;
    /// <summary>Reduce Motion accessibility setting replaces ALL movement with a crossfade of this length.</summary>
    public const int ReducedCrossfadeMs = 120;

    /// <summary>Exit duration rule: ⅔ of the paired enter duration (with <see cref="Curve.Accelerate"/>).</summary>
    public static int ExitFor(int enterMs) => enterMs * 2 / 3;
}
