using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The questions asked ABOUT a node rather than of it — its floor, whether it may shrink,
/// what kind of size it wants — and the arithmetic every measurement shares.
/// <para>
/// They are here rather than spread through the families because MEASURING said so, and the first
/// measurement was wrong in a way worth recording: taken before the flex pass became its own file,
/// it reported that none of the fifteen crossed. Re-run against the files as they actually stand,
/// TWO do — <c>DeclaredSizeKind</c> from Containers and Flex (it was <c>CrossSizeKind</c> then),
/// <c>ResolveSelf</c> from Containers and the doors. (S4 found exactly two crossing helpers as well,
/// and kept them on its class file.)
/// </para>
/// <para>
/// Thirteen of fifteen having a single caller is still not a placement: filing each beside it would
/// put the whole resolve-and-clamp arithmetic inside the flex pass, and then the two that cross
/// would have to live somewhere else anyway. The subject is what groups them — the questions asked
/// ABOUT a node, and the arithmetic every measurement shares whether or not it shares it yet.
/// </para></summary>
internal sealed partial class MeasureVisitor
{
    /// <summary>
    /// MIN-CONTENT width — the floor CSS puts under every flex item (<c>min-width: auto</c>), and
    /// the reason a browser shrinks the stretchy item rather than squashing the word next to it.
    /// For text it is the longest WORD (a word never breaks); for a row it is the sum of its
    /// children's floors plus the gaps; for a column, the widest of them. Computed only when a
    /// line actually overflows, so the common path never pays for it.
    /// </summary>
    private float MinContentWidth(VisualNode node, Element? element, LayoutContext ctx) => node switch
    {
        Text text => LongestWordWidth(text, ctx),
        Box box => box.Style.Width.Kind == SizeKind.Fixed
            ? box.Style.Width.Value
            : (box.Child is null ? 0 : MinContentWidth(box.Child, element?.ChildAt(0), ctx)) + InsetsOf(box.Style).Horizontal,
        Row row => RowMinContent(row, element, ctx),
        Column column => ColumnMinContent(column, element, ctx),
        Image image => image.Width,
        Icon icon => icon.Size,
        Vector vector => vector.Size,
        // A drawing's floor is its dp width, or nothing when it fills — a canvas's rule.
        Drawing drawing => drawing.Width.Kind == SizeKind.Fixed ? drawing.Width.Value : 0,
        // A canvas's floor is whatever it was told to be, or nothing when it fills.
        Canvas canvas => canvas.Width.Kind == SizeKind.Fixed ? canvas.Width.Value : 0,
        Spinner spinner => spinner.Size,
        CameraPreview camera => camera.Width,
        Spacer spacer => spacer.FixedLength,
        // A TRANSPARENT wrapper's floor IS its child's, and the three that are not floor at zero:
        // a scroller scrolls instead of growing, an Overlay takes no space in the flow, a
        // Positioned is a contract with a Stack. Which is which is stated ONCE, in
        // `LayoutTransparency` — this used to be a hand-kept list of five that disagreed with the
        // cross-axis classifier's hand-kept list of three (#225).
        SingleChildNode wrapper =>
            wrapper.IsLayoutTransparent() ? MinContentWidth(wrapper.Child, element?.ChildAt(0), ctx) : 0,
        // What the component's element BUILT, as measure reads it: intrinsic sizing used to build
        // a third time without reconciling, so a stateful child's floor was a fresh instance's.
        UiComponent component => element is ComponentElement { } built && built.ChildAt(0) is { } subtree
            ? MinContentWidth(subtree.Configuration, subtree, ctx)
            : throw new InvalidOperationException(
                $"The floor of {component.GetType().Name} was asked with no subtree built for it."),
        _ => 0,
    };

    private float RowMinContent(Row row, Element? element, LayoutContext ctx)
    {
        if (row.Width.Kind == SizeKind.Fixed) return row.Width.Value;
        var total = row.Padding.Horizontal + row.Gap * MathF.Max(0, row.Children.Count - 1);
        for (var i = 0; i < row.Children.Count; i++)
            total += MinContentWidth(row.Children[i], element?.ChildAt(i), ctx);
        return total;
    }

    private float ColumnMinContent(Column column, Element? element, LayoutContext ctx)
    {
        if (column.Width.Kind == SizeKind.Fixed) return column.Width.Value;
        var widest = 0f;
        for (var i = 0; i < column.Children.Count; i++)
            widest = MathF.Max(widest, MinContentWidth(column.Children[i], element?.ChildAt(i), ctx));
        return widest + column.Padding.Horizontal;
    }

    /// <summary>The widest single word — text wraps between words and never inside one.</summary>
    private float LongestWordWidth(Text text, LayoutContext ctx)
    {
        var style = text.Resolve(ctx.Theme).AtWindow(ctx.WindowWidth);
        var widest = 0f;
        foreach (var word in text.PlainContent.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            widest = MathF.Max(widest,
                ctx.Measurer.Measure(word, style, ctx.TypeScale, float.PositiveInfinity, 1).Width);
        return widest;
    }

    /// <summary>
    /// Whether a flex item gives up width when the line overflows. CSS shrinks every item by
    /// default; what it never shrinks is a size the AUTHOR pinned — so a Fixed extent and a Spacer
    /// stay put.
    /// </summary>
    private bool Shrinkable(VisualNode child) => child switch
    {
        Text => false,                       // already asked, above
        Spacer => false,
        // A Flexible gives space back from its START by its own Shrink, whatever its weight, as
        // `flex: 1 1 540px` and `flex: 0 1 540px` both do in CSS: a zero weight from its basis or its
        // content (#680), a weighted one from its basis, and a weighted one without a basis starts
        // from nothing, so it has nothing to give.
        Flexible flexible => flexible.Shrink > 0,
        Box box => box.Style.Width.Kind != SizeKind.Fixed,
        FlexNode flex => flex.Width.Kind != SizeKind.Fixed,
        // NO WRAPPER ARM, and that is measured rather than left out. #225 assumed four readers
        // needed the transparency statement; this is the one that does not, twice over.
        //
        // It is already answered by the FLOOR: an item whose min-content floor equals its main size
        // has no room to give, which is what "not shrinkable" does operationally — so
        // `Pressable(FixedBox(120))` keeps its 120 through MinContentWidth looking through, with or
        // without an arm here. Across a battery of 168 rows (four wrappers x seven child kinds x
        // three widths x either order) adding one changed exactly one family of cases.
        //
        // And it changed that one for the WORSE. `Flexible` is excluded above because its size came
        // from the row's leftover — a contract with the DIRECT parent, like a Positioned's with a
        // Stack. A wrapper is not that parent and was granted nothing, so inheriting the exclusion
        // inherits a promise nobody made: `Pressable(Flexible(Fill))` kept 100/150/220 in rows of
        // 100/150/220 that already held a 120 sibling, where the BARE Flexible yields 0/22/92. The
        // arm made the wrapped one disagree with its child, which is the opposite of transparency.
        _ => true,
    };

    /// <summary>
    /// What an item of a single-line column may shrink to, or null when it keeps the height it
    /// measured. That height is its min-content height (<c>min-height: auto</c>) unless the web
    /// writes the minimum itself: <c>min-height: 0</c> (or the box's own <c>MinHeight</c>) on a box or
    /// a flex container that fills the column, and on a scroller or a table viewport, which take the
    /// room they are given. A fixed-height one is a size re-measuring cannot move (#1186), and a fixed
    /// width is the web's <c>flex-shrink: 0</c>. Asked of what the item measured to, through a component
    /// to what it built, which is what the web lowers it to.
    /// </summary>
    private static float? ColumnFloor(LayoutNode item)
    {
        var node = item;
        while (node.Source is UiComponent && node.Children.Count > 0) node = node.Children[0];
        return node.Source switch
        {
            Box { Style.Height.Kind: SizeKind.Fill, Style.Width.Kind: not SizeKind.Fixed } box => box.Style.MinHeight,
            FlexNode { Height.Kind: SizeKind.Fill, Width.Kind: not SizeKind.Fixed } => 0,
            ScrollView { Height.Kind: not (SizeKind.Fixed or SizeKind.WindowMinus) } => 0,
            TableViewport { Height.Kind: not (SizeKind.Fixed or SizeKind.WindowMinus) } => 0,
            _ => null,
        };
    }

    /// <summary>
    /// The text an item ultimately IS, seen through layout-transparent wrappers — the FOURTH reader
    /// of the same statement, and the one whose absence made the other three's disagreement
    /// invisible.
    /// <para>
    /// The truncation contract found its subjects with <c>children[i] is Text</c>, so a wrapped one
    /// was not a text as far as it was concerned. ELEVEN of the twenty wrappers therefore ran past
    /// the end of a fixed row rather than ellipsizing — <c>Pressable</c>, <c>Link</c> and
    /// <c>Hoverable</c> among them — and the rest only agreed in WIDTH: NINETEEN of the twenty
    /// wrapped to as many lines as they liked instead of being cut, every one but <c>Overlay</c>,
    /// which takes no space in the flow to begin with. #225 measured both.
    /// </para>
    /// </summary>
    private static Text? TextWithin(VisualNode node) => node switch
    {
        Text text => text,
        SingleChildNode wrapper when wrapper.IsLayoutTransparent() => TextWithin(wrapper.Child),
        _ => null,
    };

    /// <summary>
    /// The <see cref="Positioned"/> a Stack child resolves to, THROUGH any components in between.
    /// <para>
    /// `Positioned` is a contract with the parent — like a flex weight, it means nothing anywhere
    /// else — and asking `child is Positioned` missed the moment one came out of a component. The
    /// child then took the ordinary aligned path: a corner button laid out at the stack's origin
    /// instead of its corner, silently, on both targets.
    /// </para>
    /// <para>
    /// Read from the MEASURED tree rather than by rebuilding: the build already happened, through
    /// the instance store, and building a second time would hand back a different instance.
    /// </para>
    /// <para>
    /// The measured node is asked FIRST, because it can already be the Positioned when the child is
    /// not one: an AdaptiveNode measures as the arm its window resolves (spec S6). Walking only
    /// through components, a Positioned arm was laid out at the stack's alignment (#671).
    /// </para>
    /// </summary>
    private Positioned? PositionedOf(VisualNode child, LayoutNode measured)
    {
        if (child is Positioned direct) return direct;

        var node = measured;
        // Bounded: a component wrapping a component wrapping one is ordinary; a cycle is not.
        for (var hops = 0; hops <= 8; hops++)
        {
            if (node.Source is Positioned found) return found;
            if (node.Source is not UiComponent || node.Children.Count != 1) return null;
            node = node.Children[0];
        }
        return null;
    }

    private (float X, float Y) AlignOffset(Alignment align, float slackW, float slackH)
    {
        var x = ((int)align % 3) switch { 1 => slackW / 2, 2 => slackW, _ => 0f };
        var y = ((int)align / 3) switch { 1 => slackH / 2, 2 => slackH, _ => 0f };
        return (x, y);
    }

    // ---- helpers ----------------------------------------------------------------------------------

    /// <summary>
    /// The size KIND a node declares along an axis (its width when <paramref name="horizontal"/>,
    /// its height otherwise), for every node type of the vocabulary that declares one: a
    /// <see cref="SizeValue"/> it carries (a box's style, a flex container, a grid, a stack, a
    /// scroller, a canvas, a web frame, a drawing's width), or a size its constructor demands (an
    /// image, an icon, a vector, a spinner, a camera preview, a drawing's height, which follows from
    /// its width). Anything else declares nothing of its own, which reads as Hug. It is asked of the
    /// node a child MEASURED to (<see cref="SizedBy"/>), never of the child as written.
    /// <para>
    /// ONE classifier for both axes: a line asks it of its main axis, whether a Flexible's child keeps
    /// its size in the slot, and of its cross axis, whether a stretch may override the child. The
    /// cross axis had a hand-kept list of its own, and it missed five of the fourteen node types, so
    /// a fixed-height Stack, Canvas, CameraPreview, ScrollView or WebFrame was stretched where a Box
    /// kept its height (#744); the main axis's list had missed a camera preview the same way. A list
    /// does not stand alone: <c>DeclaredSizeKindCoverageTests</c> enumerates the vocabulary's node
    /// types by reflection and fails on any that declares a size of its own this does not read.
    /// </para>
    /// </summary>
    internal static SizeKind DeclaredSizeKind(VisualNode node, bool horizontal) => node switch
    {
        Box box => (horizontal ? box.Style.Width : box.Style.Height).Kind,
        FlexNode flex => (horizontal ? flex.Width : flex.Height).Kind,
        Grid grid => (horizontal ? grid.Width : grid.Height).Kind,
        Stack stack => (horizontal ? stack.Width : stack.Height).Kind,
        ScrollView scroll => (horizontal ? scroll.Width : scroll.Height).Kind,
        // A window onto a grid takes the room it is given unless it says how large it is: its hug IS
        // a fill, because the grid behind it is no size a window should take.
        TableViewport table => (horizontal ? table.Width : table.Height).Kind is SizeKind.Fixed or SizeKind.WindowMinus
            ? (horizontal ? table.Width : table.Height).Kind
            : SizeKind.Fill,
        Canvas canvas => (horizontal ? canvas.Width : canvas.Height).Kind,
        WebFrame frame => (horizontal ? frame.Width : frame.Height).Kind,
        Image or Icon or Vector or Spinner or CameraPreview => SizeKind.Fixed,
        // A drawing's width is dp or a fill of what its parent offers; its height always follows
        // from that width, so the drawing is sized on that axis whatever its width is.
        Drawing drawing => horizontal ? drawing.Width.Kind : SizeKind.Fixed,
        _ => SizeKind.Hug,
    };

    /// <summary>
    /// The node whose declared size a measured child IS: the child, or, through a node that takes
    /// its child's size (a transparent wrapper, an <see cref="Anchored"/>, a component, which is what
    /// it builds), the node inside it. That is the FIRST one: a <see cref="CodeSurface"/> holds its
    /// options and its notes after the child that sizes it, and they take no part in its size.
    /// <para>
    /// It walks the MEASURED tree, so the size classifier is asked of what was laid out rather than
    /// of what was written. That is what makes an AdaptiveNode need no answer of its own: it
    /// measures to the arm its window resolves and is never in that tree, so a child that declares
    /// its size through an arm keeps it, where asking the node as written read nothing and pinned
    /// the arm to the item. A component is the same case: the web lowers it to what it builds, so a
    /// component that builds a box 20 tall keeps 20 in a row that stretches, where asking the
    /// component read nothing and stretched it.
    /// </para>
    /// </summary>
    internal static LayoutNode SizedBy(LayoutNode measured)
    {
        var node = measured;
        while (node.Children.Count > 0
               && (node.Source is Anchored or UiComponent
                   || node.Source is SingleChildNode wrapper && wrapper.IsLayoutTransparent()))
            node = node.Children[0];
        return node;
    }

    /// <summary>
    /// A Flexible's ITEM around its measured child, the one shape the single-line slot and the
    /// wrapping pass both build. The item takes the extent its line gave it. The child keeps a main
    /// size its node declares (<see cref="DeclaredSizeKind"/> reads it as fixed or window-relative,
    /// of the node the child measured to),
    /// wider than the item or narrower, as the web keeps such a child inside the flex item and lets
    /// it overflow: a 400 box in an item of 300 is still 400, and in an item of 540 still 400. Any
    /// other child is pinned to the item: an auto or Fill child measured to it already, and a
    /// <see cref="Text"/> sizes itself to its lines, so the item is the line box it fills and aligns
    /// them in. A scroller's ceiling is applied while it measures, not here (see
    /// <see cref="LayoutConstraints.WidthIsACeiling"/>).
    /// </summary>
    private static LayoutNode FlexItem(Flexible flexible, LayoutNode child, float main, bool horizontal, LayoutContext ctx)
    {
        if (DeclaredSizeKind(SizedBy(child).Source, horizontal) is not (SizeKind.Fixed or SizeKind.WindowMinus))
            child.Bounds = horizontal ? child.Bounds with { Width = main } : child.Bounds with { Height = main };
        var item = ctx.Node(flexible, horizontal ? child.Bounds with { Width = main } : child.Bounds with { Height = main });
        item.Adopt(child);
        return item;
    }

    /// <summary>
    /// Whether a node's declared main size is a CEILING inside its container rather than a size it
    /// keeps whatever the container gives it. A <see cref="ScrollView"/>'s width is one: the web
    /// realizer writes it <c>max-width: 100%</c> beside its width, so in a 300 item a 400-wide
    /// scroller is 300 and a 100-wide one stays 100 (measured in Chrome 154). Its height is not
    /// capped there. Asked, like <see cref="DeclaredSizeKind"/>, of the node a child measured to.
    /// </summary>
    internal static bool MainSizeIsACeiling(VisualNode node, bool horizontal) => node is ScrollView && horizontal;

    /// <summary>A cap as a NUMBER, or 0 for unbounded — the one place a window-relative cap turns
    /// into dp, from the window the pass was handed rather than the space the parent had left.</summary>
    private float CapDp(SizeValue cap, float window) => cap.Kind switch
    {
        SizeKind.Fixed => cap.Value,
        // A window smaller than the inset caps at ZERO, and zero has to survive: downstream a
        // non-positive max used to mean "no cap", so a phone-sized window did not clamp the panel,
        // it FREED it — the opposite of what the declaration asks for, and only in the window
        // where it matters most. Unbounded is -1 from here on, and zero is a real cap of zero.
        SizeKind.WindowMinus => MathF.Max(0, window - cap.Value),
        SizeKind.Fill => window,
        _ => Unbounded,
    };

    /// <summary>What a resolved cap says when there is none — NOT zero, which is a cap of zero.</summary>
    private const float Unbounded = -1f;

    private float CapMax(float available, float styleMax) =>
        styleMax >= 0 ? MathF.Min(available, styleMax) : available;

    /// <summary>Max extent a child may use, given the node's own size request.</summary>
    private float ResolveForChild(SizeValue size, float available) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        _ => available,
    };

    /// <summary>A window-relative SIZE — the same arithmetic the caps do, for a node that asks to
    /// BE the window less an inset rather than to be capped by it. Without this the kind resolved
    /// only as a cap, and `Width = WindowMinus(24)` hugged natively while the web sized it.</summary>
    private float WindowSize(SizeValue size, float window) => MathF.Max(0, window - size.Value);

    /// <summary>The size a node asks to BE, with the window in hand — the window-relative kind is
    /// the one that cannot be answered from the available space alone.</summary>
    private float ResolveSelf(SizeValue size, float available, float hug, float window,
        bool indeterminate = false, bool stretched = false) => size.Kind == SizeKind.WindowMinus
        ? WindowSize(size, window)
        : ResolveSelf(size, available, hug, indeterminate, stretched);

    /// <summary>Own size: explicit &gt; Fill &gt; Hug (spec A1). On an INDETERMINATE axis — one the
    /// parent is sizing from its content — Fill has nothing to fill and falls back to Hug.</summary>
    private float ResolveSelf(SizeValue size, float available, float hug, bool indeterminate = false,
        bool stretched = false) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        SizeKind.Fill when !indeterminate && !float.IsPositiveInfinity(available) => available,
        // Stretched by the parent: an auto size becomes the size the parent decided.
        SizeKind.Hug when stretched && !indeterminate && !float.IsPositiveInfinity(available) => available,
        _ => hug,
    };

    /// <summary>Min is a dp (0 = none); MAX follows the resolved-cap convention — negative is
    /// unbounded, and zero is a real cap of zero. See <see cref="Unbounded"/>.</summary>
    private float Clamp(float value, float min, float max)
    {
        if (min > 0) value = MathF.Max(value, min);
        if (max >= 0) value = MathF.Min(value, max);
        return value;
    }
}
