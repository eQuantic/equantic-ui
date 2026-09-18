using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The questions asked ABOUT a node rather than of it — its floor, whether it may shrink,
/// what kind of size it wants — and the arithmetic every measurement shares.
/// <para>
/// They are here rather than spread through the families because MEASURING said so, and the first
/// measurement was wrong in a way worth recording: taken before the flex pass became its own file,
/// it reported that none of the fifteen crossed. Re-run against the files as they actually stand,
/// TWO do — <c>CrossSizeKind</c> from Containers and Flex, <c>ResolveSelf</c> from Containers and
/// the doors. (S4 found exactly two crossing helpers as well, and kept them on its class file.)
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
    private float MinContentWidth(VisualNode node, LayoutContext ctx) => node switch
    {
        Text text => LongestWordWidth(text, ctx),
        Box box => box.Style.Width.Kind == SizeKind.Fixed
            ? box.Style.Width.Value
            : (box.Child is null ? 0 : MinContentWidth(box.Child, ctx)) + box.Style.Padding.Horizontal,
        Row row => RowMinContent(row, ctx),
        Column column => ColumnMinContent(column, ctx),
        Image image => image.Width,
        Icon icon => icon.Size,
        Vector vector => vector.Size,
        Drawing drawing => drawing.Width,
        // A canvas's floor is whatever it was told to be, or nothing when it fills.
        Canvas canvas => canvas.Width.Kind == SizeKind.Fixed ? canvas.Width.Value : 0,
        Spinner spinner => spinner.Size,
        CameraPreview camera => camera.Width,
        Spacer spacer => spacer.FixedLength,
        // A TRANSPARENT wrapper's floor IS its child's, and the three that are not floor at zero:
        // a scroller scrolls instead of growing, an Overlay takes no space in the flow, a
        // Positioned is a contract with a Stack. Which is which is stated ONCE, in
        // `LayoutTransparency` — this used to be a hand-kept list of five that disagreed with
        // CrossSizeKind's hand-kept list of three (#225).
        SingleChildNode wrapper =>
            wrapper.IsLayoutTransparent() ? MinContentWidth(wrapper.Child, ctx) : 0,
        // Same rule as MeasureComponent's: the visitor travels in the state so the lambda stays
        // `static` and allocates nothing.
        UiComponent component => component.ExpandContained(ctx.Components, (self: this, ctx),
            static (built, state) => state.self.MinContentWidth(built, state.ctx)),
        _ => 0,
    };

    private float RowMinContent(Row row, LayoutContext ctx)
    {
        if (row.Width.Kind == SizeKind.Fixed) return row.Width.Value;
        var total = row.Padding.Horizontal + row.Gap * MathF.Max(0, row.Children.Count - 1);
        foreach (var child in row) total += MinContentWidth(child, ctx);
        return total;
    }

    private float ColumnMinContent(Column column, LayoutContext ctx)
    {
        if (column.Width.Kind == SizeKind.Fixed) return column.Width.Value;
        var widest = 0f;
        foreach (var child in column)
            widest = MathF.Max(widest, MinContentWidth(child, ctx));
        return widest + column.Padding.Horizontal;
    }

    /// <summary>The widest single word — text wraps between words and never inside one.</summary>
    private float LongestWordWidth(Text text, LayoutContext ctx)
    {
        var style = text.Resolve(ctx.Theme);
        var widest = 0f;
        foreach (var word in text.PlainContent.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            widest = MathF.Max(widest,
                ctx.Measurer.Measure(word, style, ctx.TypeScale, float.PositiveInfinity, 1).Width);
        return widest;
    }

    /// <summary>
    /// Whether a flex item gives up width when the line overflows. CSS shrinks every item by
    /// default; what it never shrinks is a size the AUTHOR pinned — so a Fixed extent, a Spacer
    /// and a Flexible (which is sized from leftover, not from content) all stay put.
    /// </summary>
    private bool Shrinkable(VisualNode child) => child switch
    {
        Text => false,                       // already asked, above
        Spacer or Flexible => false,
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
    /// </summary>
    private Positioned? PositionedOf(VisualNode child, LayoutNode measured)
    {
        if (child is Positioned direct) return direct;

        var node = measured;
        // Bounded: a component wrapping a component wrapping one is ordinary; a cycle is not.
        for (var hops = 0; hops < 8 && node.Source is UiComponent && node.Children.Count == 1; hops++)
        {
            node = node.Children[0];
            if (node.Source is Positioned found) return found;
        }
        return null;
    }

    private (float X, float Y) AlignOffset(Alignment align, float slackW, float slackH)
    {
        var x = ((int)align % 3) switch { 1 => slackW / 2, 2 => slackW, _ => 0f };
        var y = ((int)align / 3) switch { 1 => slackH / 2, 2 => slackH, _ => 0f };
        return (x, y);
    }

    private SizeKind WidthKind(VisualNode node) => node switch
    {
        Box box => box.Style.Width.Kind,
        FlexNode flex => flex.Width.Kind,
        Grid grid => grid.Width.Kind,
        _ => SizeKind.Hug,
    };

    // ---- helpers ----------------------------------------------------------------------------------

    /// <summary>The child's declared size KIND on the flex cross axis (wrappers look through to their
    /// content; components can't be known without building — treated as Hug, i.e. stretchable).</summary>
    private SizeKind CrossSizeKind(VisualNode node, bool horizontal) => node switch
    {
        Box box => (horizontal ? box.Style.Height : box.Style.Width).Kind,
        FlexNode flex => (horizontal ? flex.Height : flex.Width).Kind,

        // Always-explicit nodes: their constructors demand a size — stretch must never override.
        Image => SizeKind.Fixed,
        Icon => SizeKind.Fixed,
        Vector => SizeKind.Fixed,
        Drawing => SizeKind.Fixed,
        Spinner => SizeKind.Fixed,
        Grid grid => (horizontal ? grid.Height : grid.Width).Kind,
        Anchored anchored => CrossSizeKind(anchored.Anchor, horizontal),
        // The same statement the floor reads, so the two can no longer drift: a transparent wrapper
        // delegates, and one that carries its own geometry hugs.
        SingleChildNode wrapper => wrapper.IsLayoutTransparent()
            ? CrossSizeKind(wrapper.Child, horizontal)
            : SizeKind.Hug,
        _ => SizeKind.Hug,
    };

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
