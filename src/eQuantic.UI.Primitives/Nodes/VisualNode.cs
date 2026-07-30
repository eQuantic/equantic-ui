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

    /// <summary>Spec S5: style DIFF applied while the pointer hovers (never fires on touch) —
    /// CSS <c>:hover</c> on web, the pointer-over interaction on Photon. <c>null</c> = none.</summary>
    public StyleDiff? Hover { get; init; }

    /// <summary>Spec S5: style DIFF applied while focused — CSS <c>:focus-visible</c> on web,
    /// the focus interaction on Photon. <c>null</c> = none.</summary>
    public StyleDiff? Focus { get; init; }
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

    public bool IsEmpty =>
        Background is null && BorderColor is null && BorderWidth is null
        && Elevation is null && Opacity is null;
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
}

/// <summary>
/// The engine fence's gradient, exactly: TWO color stops on a straight axis. Stops are TOKENS
/// (mode-free trees); realizers resolve per mode — web as <c>linear-gradient(to right|bottom, …)</c>,
/// native as <c>Paint.Linear</c> across the box bounds.
/// </summary>
public readonly record struct LinearGradient(
    ColorToken From,
    ColorToken To,
    GradientDirection Direction = GradientDirection.ToRight);

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

    /// <summary>Keyboard submit (Enter / the keyboard's return action).</summary>
    public Action? OnSubmit { get; init; }

    /// <summary>Focus transitions — the composing component's state hook (focused border etc.).</summary>
    public Action<bool>? OnFocusChanged { get; init; }

    public bool Disabled { get; init; }

    /// <summary>Password entry: glyphs render obscured (web <c>type=password</c>).</summary>
    public bool Obscure { get; init; }

    /// <summary>Type role of the entry text (spec: TextInput rides BodyL, SearchField BodyM).</summary>
    public TypeRole Role { get; init; } = TypeRole.BodyL;
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
    /// SYSTEM COMPONENTS ONLY: overrides the role's <see cref="TypeStyle"/> with an exact style from a
    /// design-system table (e.g. the Button label sizes 13/15/16/17 per spec A12). App code uses roles —
    /// free-form font sizes remain outside the component API (spec A8).
    /// </summary>
    public TypeStyle? StyleOverride { get; init; }
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

    public Flexible(VisualNode child, int flex = 1)
    {
        Child = child;
        Flex = Math.Max(1, flex);
    }

    public VisualNode Child { get; init; }
    public int Flex { get; init; }

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

/// <summary>
/// Z-axis composition (spec A3): paint order = child order (last on top), hit-testing walks
/// top-down, and the stack SIZES TO ITS LARGEST NON-POSITIONED child (explicit Width/Height
/// override). Non-positioned children align by <see cref="Align"/>; <see cref="Positioned"/>
/// children anchor to the stack's edges with signed offsets.
/// </summary>
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

    /// <summary>The variant for a class — missing variants fall back toward Compact.</summary>
    public VisualNode Resolve(WindowSizeClass sizeClass) => sizeClass switch
    {
        WindowSizeClass.Expanded => Expanded ?? Medium ?? Compact,
        WindowSizeClass.Medium => Medium ?? Compact,
        _ => Compact,
    };
}

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

/// <summary>
/// A scrolling viewport (spec A6) — BOUNDED content only (virtualized lists are the List component).
/// The child lays out UNBOUNDED on the scroll axis and is clipped to the viewport. v1 fences: the
/// platform physics (decay/fling/rubber-band), gesture capture and the fading scrollbar pill join
/// with the native interaction system; today the scroll position is the programmatic
/// <see cref="Offset"/> (web realizes as native browser scrolling, which owns its own physics).
/// </summary>
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
}

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
}
