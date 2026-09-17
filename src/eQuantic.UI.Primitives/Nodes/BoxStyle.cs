namespace eQuantic.UI.Primitives;

/// <summary>
/// Box appearance + sizing (spec A1) — the engine fence surfaced as style: solid background,
/// 2-stop linear gradient, inside border, per-corner radius, Elevation shadow, rrect Clip.
/// Opacity groups join as the engine grows that primitive (speced, deliberately not stubbed).
/// </summary>
public readonly record struct BoxStyle
{
    // Required by CS8983: a struct with property initializers (HaloBlur's default) must declare
    // its parameterless constructor explicitly.
    public BoxStyle() { }

    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }
    public float MinWidth { get; init; }
    public float MinHeight { get; init; }
    /// <summary>
    /// The widest this box may be — <c>Hug</c> (the default) is unbounded, a number is dp, and
    /// <see cref="SizeValue.WindowMinus"/> is the window less an inset. Unbounded is Hug and NOT
    /// zero: a zero here is a box zero wide.
    /// </summary>
    public SizeValue MaxWidth { get; init; }

    /// <summary>
    /// The tallest this box may be — <c>Hug</c> (the default) is unbounded, a number is dp, and
    /// <see cref="SizeValue.WindowMinus"/> is the window less an inset.
    /// <para>
    /// A <see cref="SizeValue"/> rather than a float because the cap an overlay needs cannot be
    /// written as a number: "as tall as the window allows" is the requirement of every menu and
    /// dialog, and any constant clips early in one window and overflows in another. A plain
    /// number still works — <c>MaxHeight = 620</c> converts — so nothing that had a cap changes.
    /// </para>
    /// </summary>
    public SizeValue MaxHeight { get; init; }

    public EdgeInsets Padding { get; init; }

    /// <summary>Solid background token; <c>null</c> = transparent (layout-only Box).</summary>
    public ColorToken? Background { get; init; }

    /// <summary>2-stop linear gradient fill (the engine fence's exact gradient primitive) — draws
    /// OVER <see cref="Background"/> when both are set (translucent stops show the solid through,
    /// the CSS background-image/background-color composition). <c>null</c> = no gradient.</summary>
    public LinearGradient? Gradient { get; init; }

    /// <summary>Repeating hairline grid drawn BELOW <see cref="Gradient"/> and above
    /// <see cref="Background"/> — the "engineering graph paper" backdrop. <c>null</c> = none.</summary>
    public GridPattern? Pattern { get; init; }

    /// <summary>Elliptical "spotlight" glow, painted ABOVE the grid and the solid background and
    /// below <see cref="Gradient"/>. <c>null</c> = none.</summary>
    public RadialGradient? Glow { get; init; }

    public CornerRadii CornerRadius { get; init; }

    /// <summary>Uniform border width, drawn INSIDE the bounds (spec fence). 0 = no border.</summary>
    public float BorderWidth { get; init; }
    public ColorToken BorderColor { get; init; }

    /// <summary>
    /// WHICH edges the border draws on. All by default, so nothing written before this changes.
    /// <para>
    /// A rule above a section, an accent bar down the side of a callout, a table cell that shares
    /// its neighbour's line — none of those is a Divider (a Divider is a sibling BETWEEN two things,
    /// and these belong to the box itself), and the only way to draw one was a Row wrapping a
    /// one-dp Box, which is a layout lie about what the design meant.
    /// </para>
    /// <para>
    /// FENCE: with a corner radius, a partial border differs slightly between targets at the corner
    /// where a present edge meets an absent one — the web mitres it, Photon squares it. At radius 0,
    /// which is what a rule or an accent bar has, the two are identical. Use
    /// <see cref="BorderSides.All"/> for a rounded outline.
    /// </para>
    /// </summary>
    public BorderSides BorderSides { get; init; } = BorderSides.All;

    /// <summary>Elevation level 0–5 (spec §05) — the theme resolves it to the analytic ShadowSpec;
    /// exactly ONE shadow per node. Dark E1–E2 additionally want a 1dp border (component-level).</summary>
    public int Elevation { get; init; }

    /// <summary>Clips the child subtree to this Box's rrect (engine PushClip / CSS overflow:hidden) —
    /// the container side of loop motion (a sweeping segment stays inside its track). Chrome
    /// (background/border/shadow) is the shape itself and is never clipped.</summary>
    public bool Clip { get; init; }

    /// <summary>
    /// GROUP opacity 0–1 (spec S1): the whole subtree (chrome + children) composites as ONE layer at
    /// this alpha — overlapping children never double-blend (engine PushLayer / CSS opacity).
    /// <c>null</c> = fully opaque (no layer).
    /// </summary>
    public float? Opacity { get; init; }

    /// <summary>
    /// Frosted glass (spec S3): blurs the BACKDROP — everything already painted behind this box —
    /// inside its rrect by this radius (logical px), underneath the box's own background/border.
    /// CSS <c>backdrop-filter: blur()</c> on web; the engine's BackdropBlur pass split on Photon.
    /// 0 = none. FENCE (both targets): inside a group-opacity subtree the backdrop is isolated —
    /// CSS opacity creates a containing stacking context, Photon skips the command inside layers.
    /// </summary>
    public float BackdropBlur { get; init; }

    /// <summary>
    /// CUSTOM shadow (the design's <c>shadow-glow</c>, colored button glows, halos) — a full
    /// <see cref="ShadowSpec"/> (offset/blur/spread/color), distinct from <see cref="Elevation"/>'s
    /// theme-laddered neutral depth (they compose into one shadow list). A centered halo is
    /// <c>new ShadowSpec(0, blur, 0, color)</c>. Photon draws it with the same analytic rrect
    /// shadow the elevation system uses. <c>null</c> = none.
    /// </summary>
    public ShadowSpec? Shadow { get; init; }

    /// <summary>
    /// COMPOSED custom shadows, in paint order (the design's ring + projected-glow pairs:
    /// <c>0 0 0 1px ring, 0 8px 40px -8px glow</c>). Combines with <see cref="Elevation"/>,
    /// <see cref="Shadow"/> and <see cref="InsetHighlight"/> into one list. <c>null</c> = none.
    /// </summary>
    public IReadOnlyList<ShadowSpec>? Shadows { get; init; }

    /// <summary>
    /// The 1px INNER TOP highlight (the design's glossy <c>inset 0 1px 0 white/25</c> on primary
    /// buttons). Web = an inset box-shadow entry; Photon = a hairline fill clipped to the rrect's
    /// top edge. <c>null</c> = none.
    /// </summary>
    public ColorToken? InsetHighlight { get; init; }

    /// <summary>
    /// ELEMENT blur (the design's <c>blur-3xl</c> glow washes): blurs THIS box's own pixels —
    /// distinct from <see cref="BackdropBlur"/>, which blurs what lies behind. Web =
    /// <c>filter: blur()</c>; Photon fence: renders the subtree as an offscreen layer through the
    /// engine's dual-Kawase pyramid when a native consumer arrives (the machinery is the same one
    /// BackdropBlur uses). 0 = none.
    /// </summary>
    public float Blur { get; init; }

    /// <summary>
    /// Static 2D transform (spec S1), anchored at the box CENTER (the CSS default origin). PAINT
    /// ONLY — layout is untouched, exactly like CSS transforms. <c>null</c> = identity.
    /// </summary>
    public Transform2D? Transform { get; init; }

    /// <summary>
    /// Width ÷ height constraint (spec S1 — the CSS <c>aspect-ratio</c> twin): when exactly one axis
    /// is determined, the other derives from it. Both axes explicit → both win (no constraint).
    /// 0 = none.
    /// </summary>
    public float AspectRatio { get; init; }

    /// <summary>Spec S6: animates CHANGES to this box's style — hover diffs, the scrolled variant,
    /// re-rendered values — per the spec's channels/duration/easing. <c>null</c> = snap.</summary>
    public TransitionSpec? Transition { get; init; }

    /// <summary>Spec S5: style DIFF applied while the pointer hovers (never fires on touch) —
    /// CSS <c>:hover</c> on web, the pointer-over interaction on Photon. <c>null</c> = none.</summary>
    public StyleDiff? Hover { get; init; }

    /// <summary>Spec S5: style DIFF applied while focused — CSS <c>:focus-visible</c> on web,
    /// the focus interaction on Photon. <c>null</c> = none.</summary>
    public StyleDiff? Focus { get; init; }

    /// <summary>What the mouse pointer looks like over this box — the CSS <c>cursor</c> mirror.
    /// Web emits the declaration; Photon registers a cursor region the host answers from.
    /// <see cref="PointerCursor.Default"/> = inherit whatever the surface underneath says.</summary>
    public PointerCursor Cursor { get; init; }
}
