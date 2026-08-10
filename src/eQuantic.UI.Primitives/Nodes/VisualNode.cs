using System.Collections;

namespace eQuantic.UI.Primitives;

/// <summary>
/// Base of the ABSTRACT visual vocabulary (docs/SHARED-COMPONENTS-PLAN.md) — the closed set of nodes
/// shared components are authored against, written once and lowered per target by a realizer: the web
/// realizer to HtmlElement/DOM + CSS, the native realizer to Photon primitives. Nodes are immutable
/// build products (rebuilt each Build pass, diffed by the reconciler); they hold TOKENS, never
/// resolved colors, so one tree realizes in any theme mode.
/// </summary>
public abstract class VisualNode
{
    /// <summary>Reconciler identity across rebuilds (keyed diffing) — same contract as the web SDK.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// Overrides the parent flex container's <see cref="FlexNode.Cross"/> for THIS child only
    /// (spec S1 — the CSS <c>align-self</c> twin). <c>null</c> = follow the container. Ignored
    /// outside a Row/Column.
    /// </summary>
    public CrossAlign? AlignSelf { get; init; }

    /// <summary>Spec S4: how many grid COLUMNS this child spans (clamped to the row's remainder,
    /// the CSS auto-flow behavior). 0/1 = one column. Ignored outside a <see cref="Grid"/>.</summary>
    public int GridSpan { get; init; }

    /// <summary>
    /// WIRE DISCRIMINATOR: the node's kind as a stable string ("box", "row", …). Realizers that receive
    /// nodes across a serialization/transpilation boundary (the TypeScript runtime lowering) dispatch on
    /// this instead of CLR types — class names don't survive bundling. Sealed per node type.
    /// </summary>
    public abstract string NodeKind { get; }
}

/// <summary>
/// The atom (spec A1): the engine's rrect surfaced as a component — background fill, uniform INSIDE
/// border, per-corner radius, padding around one child. Every visible surface decomposes into Boxes.
/// A11y: none by default; interactive Boxes are a spec smell — use Pressable/Button.
/// </summary>
public sealed class Box : VisualNode
{
    public override string NodeKind => "box";

    public Box(BoxStyle style = default, VisualNode? child = null)
    {
        Style = style;
        Child = child;
    }

    public BoxStyle Style { get; init; }
    public VisualNode? Child { get; init; }
}

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
    /// <summary>0 = unbounded.</summary>
    public float MaxWidth { get; init; }
    /// <summary>0 = unbounded.</summary>
    public float MaxHeight { get; init; }

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

/// <summary>
/// The pointer shapes that MEAN something to the person moving the mouse (CSS names, exactly —
/// the web emits these verbatim and the shells map them to their native cursors). Deliberately
/// small; grows only when a control genuinely needs a new meaning.
/// </summary>
public enum PointerCursor : byte
{
    Default = 0,
    Pointer = 1,
    Text = 2,
    NotAllowed = 3,
    /// <summary>Excel's fill handle; precision picking.</summary>
    Crosshair = 4,
    /// <summary>A column-resize grip.</summary>
    ColResize = 5,
    /// <summary>A row-resize grip.</summary>
    RowResize = 6,
}

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

    public bool IsEmpty =>
        Background is null && BorderColor is null && BorderWidth is null
        && Elevation is null && Opacity is null && Gradient is null && BackdropBlur is null;
}

/// <summary>Style channels a <see cref="TransitionSpec"/> animates — combine freely
/// (<c>Colors | Transform</c>). <see cref="All"/> is the whole style surface.</summary>
[Flags]
public enum StyleChannels : byte
{
    None = 0,
    /// <summary>Background, border and content colors (the hover-tint channel).</summary>
    Colors = 1,
    Opacity = 2,
    Transform = 4,
    /// <summary>box-shadow — elevation swaps and glow hovers.</summary>
    Shadow = 8,
    /// <summary>Element blur and backdrop blur (the scrolled header's frosted veil).</summary>
    Filters = 16,
    /// <summary>Width/height/max bounds — the panel-morph channel.</summary>
    Size = 32,
    All = Colors | Opacity | Transform | Shadow | Filters | Size,
}

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

/// <summary>
/// A static 2D transform as COMPONENTS, not a matrix — the closed, realizable set (spec S1): applied
/// translate → rotate → scale, anchored at the element's center. Compose fluently:
/// <c>Transform2D.Rotate(3).Scale(1.02f)</c>. The web realizer emits the equivalent CSS transform
/// list; the native realizer builds the equivalent <c>Matrix2D</c> around the box center. Always
/// reach instances through the factories/combinators — <c>default</c> is not meaningful.
/// </summary>
public readonly record struct Transform2D(
    float TranslateX = 0,
    float TranslateY = 0,
    float RotationDegrees = 0,
    float ScaleX = 1,
    float ScaleY = 1)
{
    public static Transform2D Translate(float x, float y = 0) => new(TranslateX: x, TranslateY: y);
    public static Transform2D Rotate(float degrees) => new(RotationDegrees: degrees);
    public static Transform2D Scale(float uniform) => new(ScaleX: uniform, ScaleY: uniform);
    public static Transform2D Scale(float x, float y) => new(ScaleX: x, ScaleY: y);

    /// <summary>This transform with the translation components replaced.</summary>
    public Transform2D WithTranslate(float x, float y = 0) => this with { TranslateX = x, TranslateY = y };

    /// <summary>This transform with the rotation replaced.</summary>
    public Transform2D WithRotate(float degrees) => this with { RotationDegrees = degrees };

    /// <summary>This transform with the scale replaced (uniform).</summary>
    public Transform2D WithScale(float uniform) => this with { ScaleX = uniform, ScaleY = uniform };

    public bool IsIdentity =>
        TranslateX == 0 && TranslateY == 0 && RotationDegrees == 0 && ScaleX == 1 && ScaleY == 1;
}

/// <summary>Axis of a 2-stop linear gradient — the CSS keyword pair the web realizer emits; the
/// native realizer maps it to <c>Paint.Linear</c> points over the box bounds.</summary>
public enum GradientDirection : byte
{
    ToRight = 0,
    ToBottom = 1,

    /// <summary>Top-left → bottom-right. The diagonal axis every "tinted tile" uses (CSS
    /// <c>to bottom right</c>); native runs the SAME <c>Paint.Linear</c> primitive with the
    /// diagonal endpoints — no engine or shader change, the axis was always arbitrary points.</summary>
    ToBottomRight = 2,

    /// <summary>Top-right → bottom-left (CSS <c>to bottom left</c>).</summary>
    ToBottomLeft = 3,
}

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

/// <summary>
/// A two-stop ELLIPTICAL radial fill — the "spotlight" glow marketing heroes wash behind their
/// content. Closed shape like <see cref="GridPattern"/>: two token stops, an elliptical extent, a
/// center expressed as FRACTIONS of the box (the CSS percentage form), which keeps it
/// resolution-independent and realizable on both targets. Web lowers to <c>radial-gradient()</c>;
/// native runs <c>Paint.Radial</c>, whose normalized-elliptical-distance math the shader mirrors.
/// </summary>
/// <param name="From">Color at the center.</param>
/// <param name="To">Color reached at the ellipse boundary (usually the transparent twin of From).</param>
/// <param name="CenterX">Center as a fraction of the box WIDTH (0 = left, 1 = right).</param>
/// <param name="CenterY">Center as a fraction of the box HEIGHT (0 = top, 1 = bottom).</param>
/// <param name="RadiusX">Horizontal extent in dp.</param>
/// <param name="RadiusY">Vertical extent in dp.</param>
public readonly record struct RadialGradient(
    ColorToken From,
    ColorToken To,
    float CenterX,
    float CenterY,
    float RadiusX,
    float RadiusY);

/// <summary>Loop-motion effects (spec §06: animate transform &amp; opacity ONLY — these are all transform).</summary>
public enum LoopEffect : byte
{
    /// <summary>Horizontal translate loop; offsets are FRACTIONS OF THE NODE'S OWN WIDTH, so both
    /// realizers resolve them without knowing the parent (CSS translateX(%) has the same base).</summary>
    SlideX = 0,
}

/// <summary>
/// Continuous transform-only loop motion around one child (spec §06) — the indeterminate-progress /
/// shimmer building block. Deliberately a FUNCTION OF TIME, not a stateful animator: the native
/// realizer resolves the offset from the frame clock (pure, deterministic, golden-testable at a fixed
/// t) and re-renders while active; the web realizer lowers to generated CSS keyframes (the browser
/// owns the clock; `prefers-reduced-motion` statically disables it — the spec's Reduce Motion rule).
/// </summary>
public sealed class LoopMotion : VisualNode
{
    public override string NodeKind => "loopMotion";

    public LoopMotion(VisualNode child, LoopEffect effect, float fromX, float toX, int durationMs)
    {
        Child = child;
        Effect = effect;
        FromX = fromX;
        ToX = toX;
        DurationMs = durationMs;
    }

    public VisualNode Child { get; init; }
    public LoopEffect Effect { get; init; }

    /// <summary>Loop start offset as a fraction of the node's own width (e.g. -0.35 = -35%).</summary>
    public float FromX { get; init; }

    /// <summary>Loop end offset as a fraction of the node's own width.</summary>
    public float ToX { get; init; }

    /// <summary>Loop period. The motion is linear and repeats seamlessly from <see cref="FromX"/>.</summary>
    public int DurationMs { get; init; }

    /// <summary>Reduce Motion policy: <c>true</c> hides the subtree entirely at rest (decorative
    /// overlays like the Skeleton shimmer — spec B16's Reduce Motion IS the plain placeholder);
    /// <c>false</c> renders it at its natural position (an indeterminate bar keeps a still segment).</summary>
    public bool HideAtRest { get; init; }
}

/// <summary>
/// The VIEWPORT LAYER (Phase C infrastructure): the child escapes the page flow and realizes
/// against the viewport, painted ABOVE everything in the page pass — web lowers to a generated
/// fixed inset-0 layer, native defers the subtree to an overlay pass after the page (painter's
/// order) and lays it out against the viewport. The child owns its own composition (scrim,
/// centering, sheets) from the ordinary vocabulary — Overlay is only the layer. Declarative:
/// presence in the build shows it (`if (_confirming) … new Overlay(…)`), state removes it.
/// </summary>
public sealed class Overlay : VisualNode
{
    public override string NodeKind => "overlay";

    public Overlay(VisualNode child)
    {
        Child = child;
    }

    public VisualNode Child { get; init; }

    /// <summary>False = a NON-MODAL layer (toasts): pointer input passes through everywhere except
    /// the layer's own pressables. Native is passthrough by construction (only registered regions
    /// hit); the web realizer lowers the pointer-events variant. Default TRUE (dialogs, sheets).</summary>
    public bool Modal { get; init; } = true;

    /// <summary>Visibility while <see cref="Motion"/> is set — a CLOSED layer stays mounted and
    /// animates out. Ignored without Motion (the caller renders the Overlay conditionally).</summary>
    public bool Open { get; init; } = true;

    /// <summary>
    /// OPEN/CLOSE motion (the drawer and command-palette contract, the <see cref="Anchored.Motion"/>
    /// twin): the layer stays MOUNTED across both states — so element identity survives and the
    /// transition actually runs — and fades between them; closed ends <c>visibility:hidden</c>, out
    /// of hit-testing and the focus order. <c>null</c> = mount/unmount (the historical snap). Keep
    /// building the layer's content while closed: an emptied layer collapses mid-fade.
    /// Photon fence: the overlay animator (native snaps until it lands).
    /// </summary>
    public TransitionSpec? Motion { get; init; }

    // WEB FENCE (no portal, v1): the layer is `position: fixed` where it sits in the tree, so an
    // ANCESTOR that creates a stacking context (a Stack cell's paint-order z-index, a transform, a
    // filter) traps it — the layer then stacks inside that ancestor instead of over the page. Keep
    // Overlays out of such subtrees (a Column is layout-neutral and safe) until they portal to the
    // document root. Native has no such rule: overlay layers queue against the viewport.</summary>
}

/// <summary>Where an <see cref="Anchored"/> panel attaches relative to its anchor (wave 3 v1: the
/// four corner placements; centered variants and viewport flip/clamp are the positioning fence).</summary>
public enum AnchorPlacement : byte
{
    BottomStart = 0,
    BottomEnd = 1,
    TopStart = 2,
    TopEnd = 3,
    /// <summary>Wave 3b: centered under the anchor (tooltips, hints).</summary>
    BottomCenter = 4,
    /// <summary>Wave 3b: centered above the anchor (the tooltip default).</summary>
    TopCenter = 5,
}

/// <summary>
/// The ANCHORED overlay (wave 3): a floating <see cref="Panel"/> positioned relative to the
/// in-flow <see cref="Anchor"/> — the primitive under menus, selects and popovers. The anchor owns
/// layout (the node is layout-transparent); the panel exists only while <see cref="Open"/> and
/// paints ABOVE the page. When <see cref="OnDismiss"/> is set, an invisible viewport scrim behind
/// the panel consumes the outside tap and fires it (tap-outside-closes). Web realizes as a
/// position:relative host with an absolute panel — pure CSS, no JS positioning, SSR-exact; native
/// queues a synthetic overlay layer with the panel <c>Positioned</c> from the anchor's absolute
/// bounds. v1 fences: viewport flip/clamp (a panel near the edge does not reposition), escape from
/// clipping ancestors on web (keep anchors out of overflow-hidden scrollers), Esc-key dismissal
/// (a11y system).
/// </summary>
public sealed class Anchored : VisualNode
{
    public override string NodeKind => "anchored";

    /// <summary>Default distance (dp) between the anchor's edge and the panel.</summary>
    public const float DefaultGap = 4;

    public Anchored(VisualNode anchor, VisualNode panel)
    {
        Anchor = anchor;
        Panel = panel;
    }

    public VisualNode Anchor { get; init; }

    /// <summary>The floating content. Built unconditionally so the tree shape is stable across
    /// open/close (reconciler identity); realizers emit it only while <see cref="Open"/>.</summary>
    public VisualNode Panel { get; init; }

    public AnchorPlacement Placement { get; init; } = AnchorPlacement.BottomStart;

    /// <summary>Distance (dp) between anchor and panel on the placement axis.</summary>
    public float Gap { get; init; } = DefaultGap;

    /// <summary>Controlled visibility — components own the state (Menu/Select internally,
    /// Popover through its caller).</summary>
    public bool Open { get; init; }

    /// <summary>Fired by the invisible outside-tap scrim; null = no scrim (panel only closes
    /// through its own actions).</summary>
    public Action? OnDismiss { get; init; }

    /// <summary>The panel refuses to be narrower than the anchor (the Select contract) —
    /// CSS <c>min-width:100%</c> of the host; native wraps the panel in a MinWidth box.</summary>
    public bool MatchAnchorWidth { get; init; }

    /// <summary>Wave 3b (the Tooltip mechanism): the panel shows while the POINTER hovers the
    /// anchor — pure CSS on web (the generated .eq-hoverreveal rules, zero JS), the host's hover
    /// pipeline on native. No scrim (hover leaves = closed); never fires on touch. Composes with
    /// <see cref="Open"/> (either shows the panel).</summary>
    public bool OpenOnHover { get; init; }

    /// <summary>
    /// Visual style for the outside-tap scrim (mega-menu page dimming): when set (and
    /// <see cref="OnDismiss"/> is wired), the viewport-covering scrim PAINTS instead of being
    /// invisible — background/gradient/backdrop-blur compose exactly like a Box. Ignored for
    /// hover-open panels (they have no scrim). <c>null</c> = the historical invisible scrim.
    /// </summary>
    public BoxStyle? ScrimStyle { get; init; }

    /// <summary>The open/close lift distance (dp) when <see cref="Motion"/> is set — a nudge
    /// toward the anchor, not a journey (spec §06's Presence rule, halved).</summary>
    public const float MotionLiftDp = 8;

    /// <summary>
    /// OPEN/CLOSE motion (the mega-menu contract): when set, the panel and the scrim stay MOUNTED
    /// across open/close — the tree keeps its shape, so both states share element identity — and
    /// visibility glides per this spec: the panel fades while lifting <see cref="MotionLiftDp"/>
    /// toward the anchor, the scrim cross-fades alongside. A closed panel ends
    /// <c>visibility:hidden</c> — outside hit-testing and the focus order (pure CSS: opacity/
    /// transform/visibility transitions; no JS). Photon fence: the overlay animator (panels keep
    /// snapping until it lands). <c>null</c> = mount/unmount (the historical snap). Keep building
    /// the LAST panel content while closed — an emptied panel would collapse mid-fade.
    /// </summary>
    public TransitionSpec? Motion { get; init; }
}

/// <summary>
/// POINTER-PRESENCE callback (spec S5's programmable side): fires <see cref="OnChanged"/> with
/// <c>true</c> when the pointer enters the child's bounds and <c>false</c> when it leaves — the
/// primitive under hover-intent menus (open on hover, grace-period close), hover prefetch and rich
/// hover cards. Layout-transparent; never fires on touch (there is no hover to track). Distinct
/// from <see cref="BoxStyle.Hover"/> (declarative style diff) and from <see cref="Pressable"/>
/// (press semantics) — presence COMPOSES with both by nesting. Web attaches
/// mouseenter/mouseleave; native rides the host's hover pipeline (the same one Style.Hover uses).
/// </summary>
public sealed class Hoverable : VisualNode
{
    public override string NodeKind => "hoverable";

    public Hoverable(VisualNode child, Action<bool> onChanged)
    {
        Child = child;
        OnChanged = onChanged;
    }

    public VisualNode Child { get; init; }

    /// <summary><c>true</c> = pointer entered, <c>false</c> = pointer left.</summary>
    public Action<bool> OnChanged { get; init; }
}

/// <summary>How a <see cref="Presence"/> subtree ENTERS when it first appears (spec §06).</summary>
public enum PresenceMotion : byte
{
    /// <summary>Opacity 0→1 over Motion.Base — dialog cards and scrims.</summary>
    Fade = 0,
    /// <summary>Rise from <see cref="Presence.SlideDistance"/> below while fading in — sheets, toasts.</summary>
    SlideUp = 1,
}

/// <summary>
/// ENTER MOTION for state transitions (spec §06 — the C2/C3/C4 fence): wraps a subtree whose
/// DECLARATIVE appearance (`if (_open) …`) should animate in instead of popping. Layout-transparent
/// like <see cref="LoopMotion"/> — the effect is paint-only (opacity layer + translate), so frames
/// never re-lay-out. First sighting starts the entrance; a subtree that leaves and later returns
/// replays it (presence tracking prunes departed paths). Reduce Motion replaces movement with a
/// short crossfade (<c>Motion.ReducedCrossfadeMs</c> — spec §06's static-replacement rule). Web
/// lowers to a mount-playing CSS animation class; native resolves a presence clock per layout path.
/// v1 is ENTER only: exit (removal-deferred motion) stays a fence on both targets.
/// </summary>
public sealed class Presence : VisualNode
{
    public override string NodeKind => "presence";

    /// <summary>The SlideUp rise distance (dp) — one spacing step, not a journey (spec §06).</summary>
    public const float SlideDistance = 16;

    public Presence(VisualNode child, PresenceMotion enter = PresenceMotion.Fade)
    {
        Child = child;
        Enter = enter;
    }

    public VisualNode Child { get; init; }
    public PresenceMotion Enter { get; init; }
}

/// <summary>The axis a <see cref="Draggable"/> follows. One at a time: a gesture that tracks both
/// competes with the scroll it usually lives inside.</summary>
public enum DragAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
}

/// <summary>
/// A CONTINUOUS gesture (spec S9): the child follows the finger along one axis between
/// <see cref="Min"/> and <see cref="Max"/>, and on release the caller is told where it ended so it
/// can decide what that meant. Swipe-to-reveal, pull-to-refresh and a sheet's own detents are all
/// this node plus a decision.
/// <para>
/// CONTROLLED like everything else: the node does not remember where it was left. The host tracks
/// the LIVE finger, and <see cref="RestOffset"/> — which the caller owns — is where the child glides
/// when the finger lifts. A row that stays open after a swipe is a caller that set RestOffset to
/// -96 in <see cref="OnReleased"/>; one that springs back is a caller that left it at 0.
/// </para>
/// </summary>
public sealed class Draggable : VisualNode
{
    public override string NodeKind => "draggable";

    public Draggable(VisualNode child, Action<float>? onReleased = null)
    {
        Child = child;
        OnReleased = onReleased;
    }

    public VisualNode Child { get; init; }

    public DragAxis Axis { get; init; }

    /// <summary>Travel limits along the axis. Negative is up / towards the start.</summary>
    public float Min { get; init; }
    public float Max { get; init; }

    /// <summary>Where the child sits when no finger is on it — the CALLER's state, not the node's.</summary>
    public float RestOffset { get; init; }

    /// <summary>The offset at the moment of release. The caller decides what it meant.</summary>
    public Action<float>? OnReleased { get; init; }

    /// <summary>
    /// Reported on EVERY move while the gesture is live — a slider's value has to follow the thumb,
    /// not wait for the finger to lift. Opt-in, because it costs a re-render per frame: leave it
    /// null and the offset paints without rebuilding anything, which is what a sheet wants.
    /// </summary>
    public Action<float>? OnMoved { get; init; }

    /// <summary>
    /// Report the offset as a FRACTION of the surface's own extent along the axis, rather than in
    /// dp. A slider's track is fluid — the component cannot know its pixel width, and the host can,
    /// so it converts. <see cref="Min"/> and <see cref="Max"/> are then fractions too.
    /// </summary>
    public bool Normalized { get; init; }

    /// <summary>
    /// Whether the subtree follows the finger. True for anything that IS the thing being moved — a
    /// sheet, a swiped row. False when the caller repaints the position itself from
    /// <see cref="OnMoved"/>: a slider's thumb is placed by its value, and translating it as well
    /// would move it twice.
    /// </summary>
    public bool Follows { get; init; } = true;
}

/// <summary>
/// Gestures v2 — VERTICAL drag-to-dismiss (the sheet contract): the child follows a downward drag
/// (paint-only translate — layout untouched); releasing past <see cref="ThresholdDp"/> fires
/// <see cref="OnDismiss"/> (state then removes the subtree and the EXIT motion completes from the
/// dragged position), releasing short glides back over Motion.Base. Taps inside still work — a drag
/// only engages past the Touch.PressCancelSlop, which also cancels the in-flight press. On web a
/// pointer-capture controller drives the same contract through the <c>data-eq-drag-dismiss</c>
/// marker. v1 fences: detents (partial heights), flick-velocity dismissal, horizontal axis, and
/// nested-scroll interplay.
/// </summary>
public sealed class DragDismiss : VisualNode
{
    public override string NodeKind => "dragDismiss";

    /// <summary>Release at or past this drag distance (dp) dismisses; short of it glides back.</summary>
    public const float ThresholdDp = 96;

    public DragDismiss(VisualNode child, Action? onDismiss = null)
    {
        Child = child;
        OnDismiss = onDismiss;
    }

    public VisualNode Child { get; init; }
    public Action? OnDismiss { get; init; }
}

/// <summary>
/// NAVIGATION semantics in the vocabulary: the child becomes a link to <see cref="Href"/>. The child
/// owns ALL visuals (like Pressable) — Link adds only the semantics and the interaction. Web lowers
/// to a real <c>&lt;a href&gt;</c> (SSR-crawlable; the SPA router intercepts internal clicks, so
/// guards/prefetch apply); native registers a link region and resolves a tap through the HOST's
/// navigation seam (<c>PhotonHost.NavigationRequested</c> — the platform shell maps hrefs to pages).
/// Pressables INSIDE a link win the tap (topmost dispatch), exactly like a button inside an anchor.
/// </summary>
public sealed class Link : VisualNode
{
    public override string NodeKind => "link";

    public Link(string href, VisualNode child)
    {
        Href = href;
        Child = child;
    }

    public string Href { get; init; }
    public VisualNode Child { get; init; }

    /// <summary>Accessible name when the child carries no text of its own (icon-only links).</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Single-line text ENTRY (the B9/B10 primitive): value + placeholder + change/submit/focus
/// callbacks. The web realizer lowers it to a real chrome-less <c>&lt;input&gt;</c> (the browser
/// owns caret/selection/IME); the CONTAINER chrome (border, states, label) belongs to the
/// composing component. Native renders the layout-correct one-line frame through the W4 text
/// placeholder pattern — caret/selection/IME land at M4, and the spec fixes the visual contract
/// NOW so forms don't re-layout later (spec B9).
/// </summary>
public sealed class TextEntry : VisualNode
{
    public override string NodeKind => "textEntry";

    public TextEntry(string value, Action<string>? onChanged = null)
    {
        Value = value;
        OnChanged = onChanged;
    }

    public string Value { get; init; }
    public Action<string>? OnChanged { get; init; }

    /// <summary>Shown in TextMuted while <see cref="Value"/> is empty — never a label substitute.</summary>
    public string? Placeholder { get; init; }

    /// <summary>The accessible NAME assistive tech announces (aria-label on web, the AX label on
    /// native). A placeholder disappears the moment the field holds text — it is a hint, not a
    /// name — so a field without a visible label states one here.</summary>
    public string? Label { get; init; }

    /// <summary>Keyboard submit (Enter / the keyboard's return action).</summary>
    public Action? OnSubmit { get; init; }

    /// <summary>Focus transitions — the composing component's state hook (focused border etc.).</summary>
    public Action<bool>? OnFocusChanged { get; init; }

    public bool Disabled { get; init; }

    /// <summary>Password entry: glyphs render obscured (web <c>type=password</c>).</summary>
    public bool Obscure { get; init; }

    /// <summary>Type role of the entry text (spec: TextInput rides BodyL, SearchField BodyM).</summary>
    public TypeRole Role { get; init; } = TypeRole.BodyL;

    /// <summary>
    /// Takes focus when it MOUNTS (the command-palette contract: the field is ready to type into
    /// the instant the dialog appears). Web sets the attribute AND focuses on mount — the attribute
    /// alone only fires on the initial document parse, never on a client-rendered dialog. Use once
    /// per surface; native fence: the host focus system.
    /// </summary>
    public bool Autofocus { get; init; }

    /// <summary>
    /// How many lines of text the field holds. <c>1</c> (the default) is the single-line entry;
    /// anything greater makes it a MULTI-LINE field of exactly that many lines tall — the message
    /// box of a contact form, a note, a description. The count is the field's HEIGHT, not a limit:
    /// content beyond it scrolls (web <c>textarea rows</c>). Native measures the same line count,
    /// so a form's geometry matches before the caret/IME stack lands.
    /// </summary>
    public int Lines { get; init; } = 1;
}

/// <summary>Modifier keys of a <see cref="KeyChord"/>. <see cref="Command"/> is the PLATFORM's
/// command key — ⌘ on Apple, Ctrl elsewhere — so one authored chord is right everywhere.</summary>
[Flags]
public enum KeyModifiers : byte
{
    None = 0,
    Shift = 1,
    Alt = 2,
    /// <summary>⌘ on Apple, Ctrl on Windows/Linux (the "⌘K/Ctrl+K" idiom, authored once).</summary>
    Command = 4,
    /// <summary>Literally Control, on every platform (rare — prefer <see cref="Command"/>).</summary>
    Control = 8,
}

/// <summary>
/// A key plus its modifiers. <see cref="Key"/> uses the DOM <c>KeyboardEvent.key</c> names
/// (<c>"k"</c>, <c>"Escape"</c>, <c>"ArrowDown"</c>, <c>"Enter"</c>) — one vocabulary both targets
/// map from, matched case-insensitively so Shift-typing never breaks a binding.
/// </summary>
public readonly record struct KeyChord(string Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>⌘K / Ctrl+K — the platform command chord.</summary>
    public static KeyChord Command(string key) => new(key, KeyModifiers.Command);

    public static readonly KeyChord Escape = new("Escape");
    public static readonly KeyChord Enter = new("Enter");
    public static readonly KeyChord ArrowUp = new("ArrowUp");
    public static readonly KeyChord ArrowDown = new("ArrowDown");
}

/// <summary>What the web twin ANNOUNCES an <see cref="Adjustable"/> as. Member names are the ARIA
/// tokens themselves (they cross to the client as camelCase strings — "tablist", not "tabList").</summary>
public enum AdjustableRole
{
    /// <summary>role="slider" — a continuous value the arrows nudge.</summary>
    Slider,

    /// <summary>role="tablist" — one tab selected out of a visible set.</summary>
    Tablist,

    /// <summary>role="radiogroup" — one choice out of a visible set.</summary>
    Radiogroup,
}

/// <summary>
/// ADJUSTMENT semantics in the vocabulary: the child is a control whose value the arrow keys
/// nudge — a slider, a tab strip, a segmented choice, and whatever else answers to "a little
/// more / a little less". One Tab stop for the whole control (its inner press targets stay
/// pointer-only), arrows call <see cref="OnAdjust"/> with the direction, and the web twin is
/// <see cref="Role"/> with a keydown handler — the reason this is a NODE and not component
/// wiring: both realizers need to agree on what focus means here.
/// </summary>
public sealed class Adjustable : VisualNode
{
    public override string NodeKind => "adjustable";

    public Adjustable(VisualNode child, Action<int> onAdjust)
    {
        Child = child;
        OnAdjust = onAdjust;
    }

    public VisualNode Child { get; init; }

    /// <summary>+1 for the increasing arrow, −1 for the decreasing one. The CONTROL owns what a
    /// step is worth — the keyboard only says which way; whether the ends WRAP is the control's
    /// call too (a radio group wraps like the native one, a slider clamps).</summary>
    public Action<int> OnAdjust { get; init; }

    /// <summary>Announced by assistive tech, exactly as <see cref="Pressable.Label"/> is.</summary>
    public string Label { get; init; } = "";

    /// <summary>The ARIA identity of the web twin. The native side treats every role the same —
    /// one stop, arrows adjust.</summary>
    public AdjustableRole Role { get; init; } = AdjustableRole.Slider;
}

/// <summary>
/// A KEYBOARD SHORTCUT that is live while this subtree is mounted (spec S8): the chord fires
/// <see cref="OnPressed"/> from anywhere on the page — it is not a focus-scoped handler, which is
/// exactly what a command palette (⌘K), an Esc-dismiss and a list's ↑/↓ navigation need. Mounting
/// IS the subscription: render the Shortcut only while its binding should apply (inside the open
/// dialog for Esc/arrows, at the page root for ⌘K) and unmounting removes it — no add/remove
/// listener bookkeeping in app code.
/// <para>
/// Layout-transparent (the child lowers unchanged). Web installs ONE window keydown controller that
/// dispatches to the active bindings and calls <c>preventDefault</c> when a chord matches, so ⌘K
/// never reaches the browser's own search. SSR renders the child and marks the binding on it
/// (<c>data-eq-shortcut</c>) — a keyboard shortcut needs JS by construction. Photon fence: the
/// host's key pipeline (desktop shells) — bindings are inert on native until it lands.
/// </para>
/// </summary>
public sealed class Shortcut : VisualNode
{
    public override string NodeKind => "shortcut";

    public Shortcut(VisualNode child, KeyChord chord, Action onPressed)
    {
        Child = child;
        Chord = chord;
        OnPressed = onPressed;
    }

    public VisualNode Child { get; init; }
    public KeyChord Chord { get; init; }
    public Action OnPressed { get; init; }
}

/// <summary>
/// A press-interaction surface: wraps a child, exposes an activation callback, and guarantees the
/// spec §08 hit contract — the hit rect is expanded symmetrically to at least 48×48dp even when the
/// visual is smaller (realizers register it; overlapping hit rects assert in debug).
/// </summary>
public sealed class Pressable : VisualNode
{
    public override string NodeKind => "pressable";

    public Pressable(VisualNode child, Action? onPressed = null)
    {
        Child = child;
        OnPressed = onPressed;
    }

    public VisualNode Child { get; init; }
    public Action? OnPressed { get; init; }
    public bool Disabled { get; init; }

    /// <summary>Pressed-state fill (spec §01: pressed is a REAL token swap on the same rrect — never
    /// an overlay). Framework-applied: web = generated `:active` CSS driven by a per-element custom
    /// property; native = the realizer swaps the first descendant Box fill while the press is held.
    /// Null = no pressed visual.</summary>
    public ColorToken? PressedBackground { get; init; }

    /// <summary>Accessible name (role: button). Required when the child carries no text.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// SELECTION state for a button that toggles or picks one of a set — a segmented control's
    /// segment, a filter chip, a page dot. Selection that lives only in a fill colour is invisible
    /// to assistive tech, so this states it: web lowers to <c>aria-pressed</c>, native reports it as
    /// the node's selected state. <c>null</c> = the button does not carry selection at all, which is
    /// NOT the same as <c>false</c> ("selectable, currently not selected").
    /// </summary>
    public bool? Selected { get; init; }
}

/// <summary>One inline run of a rich <see cref="Text"/> (see <see cref="Text.Spans"/>): its own
/// color and/or mono face, flowing inside the paragraph. Null color = the paragraph's color.</summary>
public sealed record TextRun(string Content, ColorToken? Color = null, bool Mono = false)
{
    /// <summary>Inline emphasis — one bold word inside a sentence, without splitting the Text.</summary>
    public FontWeight? Weight { get; init; }

    /// <summary>
    /// Where this run LINKS to — a link inside a sentence, which nothing else here can express.
    /// <para>
    /// A <see cref="Link"/> around a <see cref="Text"/> makes the whole paragraph one link, and a
    /// Row of Texts breaks between RUNS instead of between words: a sentence with three code spans
    /// wraps at the spans. So mixed emphasis has to be one Text with <see cref="Text.Spans"/> — and
    /// without this, a link in the middle of a sentence was inexpressible. For anything that reads
    /// like prose, that is most paragraphs.
    /// </para>
    /// <para>
    /// A link is a TARGET-NEUTRAL idea — a run of text that goes somewhere when you touch it, which
    /// every platform has. What is missing is realization, not meaning: the native realizer reads
    /// neither this, nor <see cref="Text.Spans"/>, nor even <see cref="Link.Href"/>, so a paragraph
    /// with links draws on Photon as its text, unlinked. Closing that needs per-run hit testing (the
    /// engine has to know each run's rect) and a navigation action a shell can answer.
    /// <para>
    /// The SHAPE is universal — an Apple <c>NSAttributedString</c> carries a <c>.link</c> attribute on
    /// a range, an Android <c>Spannable</c> carries a <c>URLSpan</c>: an inline link is a RUN with an
    /// attribute everywhere, and cannot be a separate node because a separate node is what breaks
    /// the line.
    /// <para>
    /// The NAME is not universal, and it is worth being exact about that. Apple says <c>link</c>,
    /// Android says <c>URLSpan</c>, and <c>href</c> is HTML's word. It is used here for internal
    /// consistency with <see cref="Link.Href"/>, which shipped first — one concept with two names
    /// inside one vocabulary would be worse than one borrowed name. That is a consistency argument,
    /// not a neutrality one; the abstract layer otherwise avoids a target's vocabulary on purpose
    /// (<see cref="Pressable"/>, not "Button" or "Clickable").
    /// </para>
    /// </summary>
    public string? Href { get; init; }
}

/// <summary>Line alignment within a <see cref="Text"/> paragraph (CSS <c>text-align</c> twin).</summary>
public enum TextAlignment : byte
{
    Start = 0,
    Center = 1,
    End = 2,
}

/// <summary>
/// A shaped paragraph (spec A8). Role-driven — free-form font sizes don't exist in the component API:
/// a <see cref="TypeRole"/> resolves size/weight/line-height/tracking and the Dynamic Type cap (§02).
/// Width fills the available line box and hugs when unconstrained; height = line count × line height;
/// truncation is shaping-time ellipsis honoring <see cref="MaxLines"/>.
/// </summary>
public sealed class Text : VisualNode
{
    public override string NodeKind => "text";

    public Text(string content, TypeRole role = TypeRole.BodyL, ColorToken? color = null, int maxLines = 0)
    {
        Content = content;
        Role = role;
        Color = color;
        MaxLines = maxLines;
    }

    public string Content { get; init; }
    public TypeRole Role { get; init; }

    /// <summary><c>null</c> resolves to the theme's TextPrimary.</summary>
    public ColorToken? Color { get; init; }

    /// <summary>0 = unlimited lines.</summary>
    public int MaxLines { get; init; }

    /// <summary>
    /// LINE alignment inside the paragraph's own box (CSS <c>text-align</c>): a centered display
    /// headline must center its WRAPPED lines too — container alignment only places the block.
    /// Photon fence: the native shaper positions lines start-aligned for now (single-line display
    /// text is unaffected — the box hugs).
    /// </summary>
    public TextAlignment Align { get; init; } = TextAlignment.Start;

    /// <summary>
    /// Monospace rendering (code snippets, versions, tabular figures — the design's
    /// <c>font-mono</c>). Web = the platform mono stack; Photon fence: the native rasterizer keeps
    /// the system face until <c>ITextRasterizer</c> grows a family parameter.
    /// NOTE: <see cref="Content"/> may contain <c>\n</c> — both targets honor it as a hard line
    /// break (web renders with <c>white-space: pre-line</c>; CoreText breaks paragraphs natively).
    /// </summary>
    public bool Mono { get; init; }

    /// <summary>
    /// TABULAR figures: every digit takes the same advance, so a number that changes in place does
    /// not make its row jiggle (a Stepper counting up, a table column, a live metric). The bundled
    /// face ships the <c>tnum</c> feature — this is the design system's own figure style, NOT a
    /// face swap like <see cref="Mono"/>. Photon fence: the native rasterizer keeps proportional
    /// figures until <c>ITextRasterizer</c> grows a font-features parameter.
    /// </summary>
    public bool Tabular { get; init; }

    /// <summary>
    /// RICH runs (spec A8's inline emphasis): when set, the paragraph renders these runs in order
    /// instead of <see cref="Content"/> — a highlighted figure inside a sentence
    /// (<c>"… ecosystem with **627k+** downloads …"</c>) without splitting the paragraph into
    /// blocks. Wrapping stays paragraph-level. Photon fence: the native shaper draws the
    /// concatenated text in the base style until multi-run shaping lands; keep Content as the
    /// accessible/plain twin of the runs.
    /// </summary>
    public IReadOnlyList<TextRun>? Spans { get; init; }

    /// <summary>The paragraph as PLAIN text: <see cref="Content"/>, or the runs joined — what the
    /// native shaper draws (multi-run fence) and what accessibility reads.</summary>
    public string PlainContent => Spans is { Count: > 0 } spans
        ? string.Concat(spans.Select(run => run.Content))
        : Content;

    /// <summary>
    /// SYSTEM COMPONENTS ONLY: overrides the role's <see cref="TypeStyle"/> with an exact style from a
    /// design-system table (e.g. the Button label sizes 13/15/16/17 per spec A12). App code uses roles —
    /// free-form font sizes remain outside the component API (spec A8).
    /// </summary>
    public TypeStyle? StyleOverride { get; init; }

    /// <summary>
    /// Fills the glyphs with a 2-stop gradient instead of <see cref="Color"/> (the display-headline
    /// treatment). Native tints the glyph COVERAGE with the gradient paint — the same
    /// <c>Paint.ColorAt</c> the SDF path uses; web lowers to <c>background-clip: text</c>. When set,
    /// <see cref="Color"/> is ignored.
    /// </summary>
    public LinearGradient? Gradient { get; init; }

    /// <summary>Spec S6: animates COLOR changes to this paragraph (the design's ubiquitous
    /// <c>transition-colors</c> on nav labels and links — a state swap recolors the text and the
    /// change should glide, not flip). <c>null</c> = snap.</summary>
    public TransitionSpec? Transition { get; init; }
}

/// <summary>
/// The only flow layout (spec A2): a single main axis, token Gap between children (the only legal
/// sibling spacing — it never collapses), <see cref="Flexible"/> children sharing leftover space by
/// weight. Truncation contract: TEXT children shrink to ellipsis before any sibling is pushed out;
/// fixed children (icons, avatars) never shrink.
/// </summary>
public abstract class FlexNode : VisualNode, IEnumerable<VisualNode>
{
    private readonly List<VisualNode> _children = new();

    public float Gap { get; init; }

    /// <summary>
    /// Spec S3 flow wrapping (the CSS <c>flex-wrap: wrap</c> twin): children that overflow the main
    /// extent break onto the next line. v1 scope: children keep their NATURAL main size — Flexible
    /// weights don't distribute inside a wrapping container (use a non-wrapping Row for that).
    /// </summary>
    public bool Wrap { get; init; }

    /// <summary>Spacing BETWEEN WRAPPED LINES (spec S3). <c>null</c> = same as <see cref="Gap"/>.</summary>
    public float? RunGap { get; init; }

    public MainAlign Main { get; init; } = MainAlign.Start;
    public abstract CrossAlign Cross { get; init; }
    public EdgeInsets Padding { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }
    /// <summary>Optional container background (sugar for wrapping in a Box).</summary>
    public ColorToken? Background { get; init; }
    public CornerRadii CornerRadius { get; init; }

    public IReadOnlyList<VisualNode> Children => _children;

    /// <summary>Collection-initializer support: <c>new Column(gap: Space.S4) { a, b, c }</c>.</summary>
    public void Add(VisualNode child) => _children.Add(child);

    public IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();
}

/// <summary>Horizontal flex (spec A2). Cross defaults to Center. Mirrors automatically in RTL (realizer concern).</summary>
public sealed class Row : FlexNode
{
    public override string NodeKind => "row";

    public Row(float gap = 0) => Gap = gap;
    public override CrossAlign Cross { get; init; } = CrossAlign.Center;
}

/// <summary>Vertical flex (spec A2). Cross defaults to Stretch (full-width children).</summary>
public sealed class Column : FlexNode
{
    public override string NodeKind => "column";

    public Column(float gap = 0) => Gap = gap;
    public override CrossAlign Cross { get; init; } = CrossAlign.Stretch;
}

/// <summary>Marks a flex child that shares LEFTOVER main-axis space by weight (spec A2 <c>Flex(n)</c>).</summary>
public sealed class Flexible : VisualNode
{
    public override string NodeKind => "flexible";

    public Flexible(VisualNode child, int flex = 1, float basis = 0, int shrink = 1)
    {
        Child = child;
        Flex = Math.Max(1, flex);
        Basis = MathF.Max(0, basis);
        Shrink = Math.Max(0, shrink);
    }

    public VisualNode Child { get; init; }
    public int Flex { get; init; }

    /// <summary>
    /// The size this child STARTS from before any leftover is shared — CSS <c>flex-basis</c>.
    /// <para>
    /// Zero (the default) is the historical behaviour: the child contributes nothing of its own and
    /// is sized purely from its weight. A non-zero basis is what makes a WRAPPING container work
    /// like the web's, because the basis is the size the line-breaker measures against: give two
    /// panes a basis of 440 and they sit side by side while there is room for both, then each takes
    /// a full line of its own. Without it a wrapping row could only ever place children at their
    /// natural size, which is why a responsive two-pane layout was previously inexpressible.
    /// </para>
    /// </summary>
    public float Basis { get; init; }

    /// <summary>
    /// How readily this child gives space back when the line overflows — CSS <c>flex-shrink</c>.
    /// Defaults to 1, matching what the web realizer has always emitted. Zero refuses to shrink.
    /// Shrinking is weighted by the basis, as CSS does, and never crosses the min-content floor.
    /// </summary>
    public int Shrink { get; init; }

    /// <summary>Animate WEIGHT changes at Base 200ms standard (spec B14: "value changes animate…").
    /// The composing component decides per render — forward-only contracts set it false on a
    /// regression so the change SNAPS (honesty over smoothness). Web = a flex-grow transition;
    /// native joins with the transition animator (until then weights snap, the documented fence).</summary>
    public bool AnimateChanges { get; init; }
}

/// <summary>
/// Layout-only space (spec A4): draws nothing, hits nothing, announces nothing. The flexible form
/// collapses to 0 when siblings need the space; <see cref="Fixed"/> is a rigid one-off rhythm break
/// (prefer the parent's Gap).
/// </summary>
public sealed class Spacer : VisualNode
{
    public override string NodeKind => "spacer";

    public Spacer(int flex = 1) => Flex = Math.Max(1, flex);

    private Spacer(float fixedLength)
    {
        Flex = 0;
        FixedLength = fixedLength;
    }

    public int Flex { get; init; }
    public float FixedLength { get; init; }

    /// <summary>Spec B14: weight changes animate over Motion.Base — pair with an animated Flexible
    /// so the RATIO glides (constant denominator) instead of jumping when the counterweight snaps.</summary>
    public bool AnimateChanges { get; init; }

    public static Spacer Fixed(float length) => new(length);
}

/// <summary>Nine-position alignment for <see cref="Stack"/> children (spec A3).</summary>
public enum Alignment : byte
{
    TopStart = 0, TopCenter = 1, TopEnd = 2,
    CenterStart = 3, Center = 4, CenterEnd = 5,
    BottomStart = 6, BottomCenter = 7, BottomEnd = 8,
}

/// <summary>One grid column track (spec S4): Fixed dp, Flex weight (the CSS <c>fr</c>), or Auto
/// (sized by its widest starting item).</summary>
public readonly record struct GridTrack(SizeKind Kind, float Value)
{
    public static GridTrack Fixed(float dp) => new(SizeKind.Fixed, dp);
    public static GridTrack Flex(float weight = 1) => new(SizeKind.Fill, weight);
    public static GridTrack Auto => new(SizeKind.Hug, 0);

    /// <summary>N copies of the same track — <c>GridTrack.Repeat(3, GridTrack.Flex())</c>.</summary>
    public static GridTrack[] Repeat(int count, GridTrack track)
    {
        var tracks = new GridTrack[count];
        Array.Fill(tracks, track);
        return tracks;
    }
}

/// <summary>
/// TRUE 2D layout (spec S4 — the CSS Grid twin, v1 auto-flow): explicit column tracks, children
/// placed left→right wrapping to new rows, per-child <see cref="VisualNode.GridSpan"/>. Rows size to
/// their tallest cell. Realized as CSS Grid on the web and a track-sizing pass on Photon.
/// </summary>
public sealed class Grid : VisualNode, IEnumerable<VisualNode>
{
    private readonly List<VisualNode> _children = new();

    public override string NodeKind => "grid";

    public Grid(IReadOnlyList<GridTrack> columns, float gap = 0, float? rowGap = null)
    {
        Columns = columns;
        Gap = gap;
        RowGap = rowGap;
    }

    public IReadOnlyList<GridTrack> Columns { get; }

    /// <summary>Gap between COLUMNS. <see cref="RowGap"/> defaults to it.</summary>
    public float Gap { get; init; }
    public float? RowGap { get; init; }
    public EdgeInsets Padding { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    public IReadOnlyList<VisualNode> Children => _children;
    public void Add(VisualNode child) => _children.Add(child);
    public IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();
}

/// <summary>Spec S6 — the Material window size classes: the ONLY responsive vocabulary app code
/// speaks. Web realizes them as build-time media queries; Photon resolves from the window width.</summary>
public enum WindowSizeClass : byte
{
    /// <summary>&lt; 600dp — phones portrait.</summary>
    Compact = 0,
    /// <summary>600–839dp — tablets portrait, foldables.</summary>
    Medium = 1,
    /// <summary>≥ 840dp — tablets landscape, desktop.</summary>
    Expanded = 2,
}

public static class WindowSizeClasses
{
    public const float MediumMinDp = 600;
    public const float ExpandedMinDp = 840;

    public static WindowSizeClass FromWidth(float dp) => dp switch
    {
        >= ExpandedMinDp => WindowSizeClass.Expanded,
        >= MediumMinDp => WindowSizeClass.Medium,
        _ => WindowSizeClass.Compact,
    };
}

/// <summary>
/// Spec S6 — a subtree that ADAPTS to the window size class: up to three variants, resolved by the
/// fallback chain (Expanded → Medium → Compact). Fully general responsiveness — a different nav, a
/// different grid, a different direction — with ZERO listeners: the web realizer emits every
/// declared variant gated by build-time media queries (display:contents/none); Photon lays out only
/// the variant matching the window class and re-lays-out when the class crosses a threshold.
/// </summary>
public sealed class AdaptiveNode : VisualNode
{
    public override string NodeKind => "adaptive";

    public AdaptiveNode(VisualNode compact, VisualNode? medium = null, VisualNode? expanded = null)
    {
        Compact = compact;
        Medium = medium;
        Expanded = expanded;
    }

    public VisualNode Compact { get; }
    public VisualNode? Medium { get; }
    public VisualNode? Expanded { get; }

    /// <summary>
    /// Where <see cref="Medium"/> takes over (dp). Defaults to the spec's
    /// <see cref="WindowSizeClass"/> threshold; override when the DESIGN switches elsewhere — a bar
    /// whose nav needs 1024dp of room has no business flipping at 600.
    /// </summary>
    public float MediumFrom { get; init; } = WindowSizeClasses.MediumMinDp;

    /// <summary>Where <see cref="Expanded"/> takes over (dp). See <see cref="MediumFrom"/>.</summary>
    public float ExpandedFrom { get; init; } = WindowSizeClasses.ExpandedMinDp;

    /// <summary>The variant for a WIDTH (dp) — the general form, honoring custom thresholds.
    /// Missing variants fall back toward Compact.</summary>
    public VisualNode ResolveWidth(float widthDp)
    {
        if (Expanded is { } expanded && widthDp >= ExpandedFrom) return expanded;
        if (Medium is { } medium && widthDp >= MediumFrom) return medium;
        // A width past the Expanded threshold with no Expanded variant still wants Medium.
        if (Medium is { } fallbackMedium && widthDp >= MediumFrom) return fallbackMedium;
        return Compact;
    }

    /// <summary>The variant for a size CLASS — the spec-threshold shorthand (custom thresholds
    /// resolve through <see cref="ResolveWidth"/>).</summary>
    public VisualNode Resolve(WindowSizeClass sizeClass) => sizeClass switch
    {
        WindowSizeClass.Expanded => Expanded ?? Medium ?? Compact,
        WindowSizeClass.Medium => Medium ?? Compact,
        _ => Compact,
    };
}

/// <summary>
/// Z-axis composition (spec A3): paint order = child order (last on top), hit-testing walks
/// top-down, and the stack SIZES TO ITS LARGEST NON-POSITIONED child (explicit Width/Height
/// override). Non-positioned children align by <see cref="Align"/>; <see cref="Positioned"/>
/// children anchor to the stack's edges with signed offsets.
/// </summary>
public sealed class Stack : VisualNode
{
    public sealed override string NodeKind => "stack";

    public Stack(Alignment align = Alignment.TopStart) => Align = align;

    public Alignment Align { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }
    public List<VisualNode> Children { get; } = new();

    public void Add(VisualNode child) => Children.Add(child);
}

/// <summary>
/// Anchors a <see cref="Stack"/> child to the stack's edges (spec A3) — offsets may be negative
/// (the Badge overlay attaches at top −4 / end −4). Unset axes fall back to the stack alignment.
/// </summary>
public sealed class Positioned : VisualNode
{
    public sealed override string NodeKind => "positioned";

    public Positioned(VisualNode child, float? top = null, float? end = null,
        float? bottom = null, float? start = null)
    {
        Child = child;
        Top = top;
        End = end;
        Bottom = bottom;
        Start = start;
    }

    public VisualNode Child { get; }
    public float? Top { get; init; }
    public float? End { get; init; }
    public float? Bottom { get; init; }
    public float? Start { get; init; }

    /// <summary>Spec S7: explicit stacking inside the Stack — higher paints (and hit-tests) on top.
    /// Equal values keep declaration order (stable). 0 = flow order.</summary>
    public int ZIndex { get; init; }
}

/// <summary>Scroll axis (spec A6).</summary>
public enum ScrollAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
    /// <summary>Spec S7: scrolls on both axes (tables, canvases).</summary>
    Both = 2,
}

/// <summary>Which edges a <see cref="SafeArea"/> keeps clear. Flags, because a screen usually keeps
/// the top and bottom clear while letting content run edge to edge horizontally.</summary>
[Flags]
public enum SafeEdges : byte
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Start = 4,
    End = 8,
    Horizontal = Start | End,
    Vertical = Top | Bottom,
    All = Top | Bottom | Start | End,
}

/// <summary>
/// Keeps its child clear of the parts of the display the SYSTEM owns — the notch, the status bar,
/// the home indicator, a desktop title bar. The insets are the HOST's to report, never the app's to
/// guess: web reads <c>env(safe-area-inset-*)</c>, which the browser fills, and Photon reads them
/// from the window (zero on a desktop with no cutouts, which is the correct answer there).
/// <para>
/// It pads rather than positions, so it composes: a scroll view inside one scrolls under nothing,
/// and a bottom bar inside one sits above the home indicator instead of under it.
/// </para>
/// </summary>
public sealed class SafeArea : VisualNode
{
    public override string NodeKind => "safeArea";

    public SafeArea(VisualNode child, SafeEdges edges = SafeEdges.All)
    {
        Child = child;
        Edges = edges;
    }

    public VisualNode Child { get; init; }
    public SafeEdges Edges { get; init; }

    /// <summary>Added to whatever the host reports — a bar's own padding on top of the inset.</summary>
    public EdgeInsets Extra { get; init; }
}

/// <summary>
/// Spec S7 — scroll-anchored chrome (section headers): the child renders in flow, but PINS to the
/// start of the scroll viewport once scrolling would push it out, offset by <see cref="Offset"/>.
/// v1 scope: vertical scrolling (CSS <c>position: sticky; top</c>); the Photon pinning joins the
/// native scroll compositor when engine scrolling lands — until then it renders in flow (correct
/// at scroll offset 0).
/// </summary>
public sealed class Sticky : VisualNode
{
    public override string NodeKind => "sticky";

    public Sticky(VisualNode child, float offset = 0)
    {
        Child = child;
        Offset = offset;
    }

    public VisualNode Child { get; }

    /// <summary>Distance from the viewport's start edge while pinned (dp).</summary>
    public float Offset { get; init; }

    /// <summary>
    /// FLOATING chrome (the fixed header): the bar paints ABOVE the page at the viewport edge and
    /// takes no layout space — content slides underneath (give the first section its own top
    /// padding). CSS <c>position:fixed; inset-inline:0</c>; Photon fence: the native realizer
    /// pins floating chrome with the overlay pass when the shell lands.
    /// </summary>
    public bool Float { get; init; }

    /// <summary>
    /// SCROLL-LINKED style (the handoff's transparent-until-scrolled header): this diff applies
    /// while the window has scrolled past a small threshold. Web = a root-gated rule set
    /// (<c>html.eq-scrolled …</c>) toggled by a tiny passive listener; Photon fence: joins the
    /// host scroll compositor. <c>null</c> = none.
    /// </summary>
    public StyleDiff? ScrolledStyle { get; init; }

    /// <summary>Spec S6: animates the swap INTO and out of <see cref="ScrolledStyle"/> — without it
    /// the bar flips from transparent to veiled in one frame. <c>null</c> = snap.</summary>
    public TransitionSpec? Transition { get; init; }
}

/// <summary>
/// A scrolling viewport (spec A6) — BOUNDED content only (virtualized lists are the List component).
/// The child lays out UNBOUNDED on the scroll axis and is clipped to the viewport. v1 fences: the
/// platform physics (decay/fling/rubber-band), gesture capture and the fading scrollbar pill join
/// with the native interaction system; today the scroll position is the programmatic
/// <see cref="Offset"/> (web realizes as native browser scrolling, which owns its own physics).
/// </summary>
public sealed class ScrollView : VisualNode
{
    public sealed override string NodeKind => "scrollView";

    public ScrollView(VisualNode child, ScrollAxis axis = ScrollAxis.Vertical)
    {
        Child = child;
        Axis = axis;
    }

    public VisualNode Child { get; }
    public ScrollAxis Axis { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    /// <summary>Programmatic scroll position in dp (≥ 0, toward the content end).</summary>
    public float Offset { get; init; }

    /// <summary>
    /// Where it IS, whenever that changes — the out channel to the in channel above. A component
    /// that has to know: a list that only builds the rows you can see, a header that shrinks, a
    /// "back to top" that appears past a fold. Without it the offset lives in the host and no
    /// component can ask, which is the difference between a list that scrolls and a list that
    /// can be long.
    /// </summary>
    public Action<float>? OnScrolled { get; init; }

    /// <summary>How tall the viewport turned out to be, reported once it is known. A window over a
    /// long document is (offset, height) and neither is knowable before layout.</summary>
    public Action<float>? OnViewportChanged { get; init; }
}

/// <summary>
/// An EDITABLE code surface: the drawing its child does, plus a caret, a selection, and a keyboard.
/// <para>
/// The child is whatever renders the lines — a <c>CodeBlock</c>, in practice — and this node adds
/// nothing to the picture except the two marks that say where you are. Everything a key or a click
/// MEANS is in the controller, which both targets share, so a realizer only has to do arithmetic:
/// with a monospaced face, a (line, column) is <c>(top + line × lineHeight, left + column × columnWidth)</c>
/// and nothing has to be measured per keystroke.
/// </para>
/// <para>
/// The controller is a live object the composing component owns, so it survives the rebuild each
/// keystroke causes — which is exactly why the surface carries it rather than a copy of its state.
/// </para>
/// </summary>
public sealed class CodeSurface : VisualNode
{
    /// <summary>
    /// How long the caret holds each phase, in ms — 500 on, 500 off. Normative for BOTH targets: a
    /// window blinks it from the host's clock (which is also what paces its frames), a page from a
    /// CSS animation over twice this period.
    /// </summary>
    public const int CaretBlinkMs = 500;

    public override string NodeKind => "codeSurface";

    public CodeSurface(VisualNode child, CodeEditorController editor)
    {
        Child = child;
        Editor = editor;
    }

    public VisualNode Child { get; init; }

    /// <summary>The document, the selection, and every command that changes either.</summary>
    public CodeEditorController Editor { get; init; }

    /// <summary>Where the first line's top sits inside this surface, and how tall each line is.</summary>
    public float ContentTop { get; init; }
    public float LineHeight { get; init; } = 18;

    /// <summary>Where column 0 sits (past the gutter), and the advance of ONE character. With a
    /// fixed pitch these two numbers place every caret in the document.</summary>
    public float ContentLeft { get; init; }
    public float ColumnWidth { get; init; } = 8;

    /// <summary>Raised after any command that changed the document or the selection — the composing
    /// component's cue to SetState, because the controller mutates outside the tree.</summary>
    public Action? OnChanged { get; init; }

    /// <summary>Accessible name (role: textbox, multiline).</summary>
    public string? Label { get; init; }

    /// <summary>Takes the caret when it first appears — a panel that opens ready to type.</summary>
    public bool Autofocus { get; init; }

    /// <summary>
    /// The two marks' ink, carried HERE rather than decided by each realizer: an editor on an
    /// inverse slab writes with an ink of its own, and a caret painted from the page's theme is
    /// invisible on exactly the surface people type into. Null falls back to the theme
    /// (<c>TextPrimary</c> for the caret, <c>FocusRing</c> for the band).
    /// </summary>
    public ColorToken? CaretColor { get; init; }
    public ColorToken? SelectionColor { get; init; }
}

/// <summary>
/// An EDITABLE SPREADSHEET surface: the grid its child draws, plus Excel's selection band, the
/// active-cell ring, and the keyboard. The child is whatever renders the visible cells — the
/// shared Spreadsheet component, in practice — and this node adds the marks and the input.
/// Everything a key or a click MEANS lives in the <see cref="SheetController"/>, which both
/// targets share; the realizer only does arithmetic: with per-line sizes, a (row, col) is a
/// prefix sum over the visible window.
/// <para>
/// The controller is a live object the composing component owns, surviving the rebuild every
/// keystroke causes. <see cref="FirstRow"/>/<see cref="FirstCol"/> say which cell the child's
/// top-left is (the virtualized window's origin), so the surface's marks line up with the pixels.
/// </para>
/// </summary>
public sealed class SheetSurface : VisualNode
{
    public override string NodeKind => "sheetSurface";

    public SheetSurface(VisualNode child, SheetController controller)
    {
        Child = child;
        Controller = controller;
    }

    public VisualNode Child { get; init; }
    public SheetController Controller { get; init; }

    /// <summary>The window's origin: the sheet row/col the child's first cell renders.</summary>
    public int FirstRow { get; init; }
    public int FirstCol { get; init; }

    /// <summary>Where the GRID starts inside the child (past the row/column headers).</summary>
    public float HeaderWidth { get; init; }
    public float HeaderHeight { get; init; }

    /// <summary>Raised after any command that changed the document or the selection — the
    /// composing component's cue to SetState.</summary>
    public Action? OnChanged { get; init; }

    /// <summary>Accessible name (role: grid).</summary>
    public string? Label { get; init; }
}

