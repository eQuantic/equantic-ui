
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

    /// <summary>The screen gutter — the inset every full-width surface keeps from the edge. An
    /// alias for <see cref="S4"/> that says WHY, so a screen reads `Space.Gutter` and a card's own
    /// padding reads `Space.S4`.</summary>
    public const float Gutter = S4;
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
    /// <summary>Step 1 of 4 — inline with text.</summary>
    public const float Sm = 16;
    /// <summary>Step 2 of 4 — dense UI. Sits BETWEEN <see cref="Sm"/> and <see cref="Md"/>: the
    /// name states intent, not rank, so read the ladder as Sm &lt; Dense &lt; Md &lt; Lg.</summary>
    public const float Dense = 20;
    /// <summary>Step 3 of 4 — the default.</summary>
    public const float Md = 24;
    /// <summary>Step 4 of 4 — feature-sized.</summary>
    public const float Lg = 32;
}

/// <summary>
/// The CONTROL LADDER (spec A12) — height, horizontal padding, inner gap, label size, icon size,
/// corner radius and hit target for a control of a given <see cref="SizeVariant"/>.
/// <para>
/// This is a TOKEN, not a Button detail: a TextInput, Chip, Select, Stepper or SegmentedControl of
/// the same size must measure identically. Reach for it instead of re-typing 40.
/// </para>
/// </summary>
public static class Sizing
{
    public static float Height(SizeVariant size) => size switch
    {
        SizeVariant.Small => 32,
        SizeVariant.Medium => 40,
        SizeVariant.Large => 48,
        _ => 56,
    };

    public static float PaddingX(SizeVariant size) => size switch
    {
        SizeVariant.Small => Space.S3,
        SizeVariant.Medium => Space.S4,
        SizeVariant.Large => Space.S5,
        _ => Space.S6,
    };

    /// <summary>Gap between a control's icon and its label.</summary>
    public static float Gap(SizeVariant size) => size switch
    {
        SizeVariant.Small => 6,
        SizeVariant.Medium => Space.S2,
        SizeVariant.Large => Space.S2,
        _ => 10,
    };

    public static float LabelSize(SizeVariant size) => size switch
    {
        SizeVariant.Small => 13,
        SizeVariant.Medium => 15,
        SizeVariant.Large => 16,
        _ => 17,
    };

    public static float Icon(SizeVariant size) => size switch
    {
        SizeVariant.Small => IconSize.Sm,
        SizeVariant.Medium => IconSize.Dense,
        SizeVariant.Large => IconSize.Dense,
        _ => IconSize.Md,
    };

    public static float Radius(SizeVariant size) =>
        size == SizeVariant.XLarge ? Primitives.Radius.Lg : Primitives.Radius.Md;

    /// <summary>The hit rect: the enforced minimum, except XLarge whose visual already exceeds it.</summary>
    public static float HitTarget(SizeVariant size) =>
        size == SizeVariant.XLarge ? 56 : Touch.MinTarget;
}

/// <summary>Touch-target rules (spec §08): the engine enforces the stricter 48dp on both platforms.</summary>
public static class Touch
{
    /// <summary>Minimum hit-rect side; visuals may be smaller — the framework expands hit-slop symmetrically.</summary>
    public const float MinTarget = 48;

    /// <summary>Drag beyond this distance from the hit rect cancels a press without firing.</summary>
    public const float PressCancelSlop = 12;

    /// <summary>
    /// How far one LINE of wheel scrolling travels. A trackpad reports its scroll in points and
    /// needs no conversion; a mouse wheel reports LINES, and taking that number for points makes a
    /// notch move three pixels — the whole page then costs a hundred turns of the wheel. Sixteen is
    /// the browsers' own line, so a wheel behaves the same in a Photon window as in a page.
    /// </summary>
    public const float WheelLine = 16;

    /// <summary>
    /// How far a wheel event moves the content, in dp. A trackpad reports PRECISE deltas already in
    /// points and passes straight through; a wheel reports LINES, and each is
    /// <see cref="WheelLine"/>. Taking a line count for points is why a page moved a few pixels per
    /// notch — the rule lives here, where it can be stated and tested, rather than inside a shell's
    /// event interop where nobody can see it.
    /// </summary>
    public static float WheelTravel(float delta, bool precise) =>
        precise ? delta : delta * WheelLine;
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

    // ---- Named roles ---------------------------------------------------------------------------
    // A duration alone still leaves the curve to guesswork, and every call site that has to decide
    // invents a number off the scale. Reach for the ROLE and both come with it.

    /// <summary>Pressed and hover-like feedback — the fastest thing a user can perceive as response.</summary>
    public static readonly MotionSpec Press = new(FastMs, Curve.Standard);

    /// <summary>A state change on something already on screen: selection, tint, enable/disable.</summary>
    public static readonly MotionSpec State = new(BaseMs, Curve.Standard);

    /// <summary>A surface arriving — sheet, toast, popover, page push.</summary>
    public static readonly MotionSpec Enter = new(SlowMs, Curve.Decelerate);

    /// <summary>The paired exit: ⅔ of the enter, accelerating out.</summary>
    public static readonly MotionSpec Exit = new(ExitFor(SlowMs), Curve.Accelerate);
}

/// <summary>
/// A motion ROLE — a duration and the curve that belongs with it, so a call site names what is
/// happening instead of picking a number. The physical settle after a gesture is NOT here: releases
/// run <see cref="SpringSpec"/> with the release velocity injected, never a keyframed duration.
/// </summary>
public readonly record struct MotionSpec(int DurationMs, Curve Curve);
