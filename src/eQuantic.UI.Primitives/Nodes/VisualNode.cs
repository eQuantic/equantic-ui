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
/// Box appearance + sizing (spec A1). v1 carries what the engine renders TODAY — solid background,
/// inside border, per-corner radius; gradient backgrounds, Elevation shadows, Clip and Opacity groups
/// join as the engine grows those primitives (they are speced, deliberately not stubbed here).
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
    public CornerRadii CornerRadius { get; init; }

    /// <summary>Uniform border width, drawn INSIDE the bounds (spec fence). 0 = no border.</summary>
    public float BorderWidth { get; init; }
    public ColorToken BorderColor { get; init; }
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
}

/// <summary>Scroll axis (spec A6).</summary>
public enum ScrollAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
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
}
