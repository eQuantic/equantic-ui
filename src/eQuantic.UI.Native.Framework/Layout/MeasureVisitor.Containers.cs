using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The nodes that hold others and decide where they land: a box and its padding, a
/// stack's layers, a grid's tracks, a scroller's content, and the host's own insets.</summary>
internal sealed partial class MeasureVisitor
{
    /// <summary>
    /// Insets from the HOST (notch, status bar, home indicator) plus the caller's own padding. The
    /// host reports them; on a desktop window they are zero, which is the correct answer there.
    /// </summary>
    private LayoutNode MeasureSafeArea(SafeArea safeArea, LayoutConstraints constraints,
        LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var host = ctx.SafeAreaInsets;
        var top = (safeArea.Edges.HasFlag(SafeEdges.Top) ? host.Top : 0) + safeArea.Extra.Top;
        var bottom = (safeArea.Edges.HasFlag(SafeEdges.Bottom) ? host.Bottom : 0) + safeArea.Extra.Bottom;
        var start = (safeArea.Edges.HasFlag(SafeEdges.Start) ? host.Start : 0) + safeArea.Extra.Start;
        var end = (safeArea.Edges.HasFlag(SafeEdges.End) ? host.End : 0) + safeArea.Extra.End;
        // The window controls' corner is HORIZONTAL room only: the toolbar that asks for it IS the
        // strip they float over, so their height is its height, not a margin above it.
        if (safeArea.Edges.HasFlag(SafeEdges.WindowControls))
        {
            start += ctx.WindowControlsInsets.Start;
            end += ctx.WindowControlsInsets.End;
        }

        // Transparent to stretch, like every wrapper: the system's margins shrink the box, they
        // do not change who decides the size.
        var child = Measure(safeArea.Child, constraints.WithMax(
            MathF.Max(0, maxW - start - end), MathF.Max(0, maxH - top - bottom)), ctx, ctx.ChildPath(path, 0));
        child.Bounds = child.Bounds with { X = start, Y = top };

        var node = ctx.Node(safeArea,
            new Rect(0, 0, child.Bounds.Width + start + end, child.Bounds.Height + top + bottom));
        node.Adopt(child);
        return node;
    }

    /// <summary>Spec A3: sizes to the largest NON-positioned child (explicit Width/Height override);
    /// non-positioned children align by <see cref="Stack.Align"/>; Positioned children anchor to the
    /// resolved frame with signed offsets (unset axes fall back to the alignment). Paint order is
    /// child order — the LayoutNode children keep it.</summary>
    private LayoutNode MeasureStack(Stack stack, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = ctx.Node(stack);
        // The cell IS the stack's available space — the contract the web's grid keeps (LowerStack):
        // a Fill child covers a stack that has a size of its own. Measured against the INCOMING
        // constraints instead, a Fill canvas under a scroll view's unbounded axis measured zero
        // inside a stack standing 240dp tall — the first chart drew nothing on Photon while the
        // browser drew it. An explicit axis decides for the children, and is determinate for them
        // whatever the parent said; the other axes pass the question through unchanged.
        var childMaxW = stack.Width.Kind == SizeKind.Fixed ? stack.Width.Value : maxW;
        var childMaxH = stack.Height.Kind == SizeKind.Fixed ? stack.Height.Value : maxH;
        var outerIndeterminateW = constraints.Width.Indeterminate;
        var outerIndeterminateH = constraints.Height.Indeterminate;
        // An explicit axis DECIDES for the children and is determinate for them whatever the parent
        // said; the other axes pass the question through unchanged. Computed for the child's own
        // constraint rather than written onto the context and undone afterwards.
        var stackIndetW = stack.Width.Kind != SizeKind.Fixed && outerIndeterminateW;
        var stackIndetH = stack.Height.Kind != SizeKind.Fixed && outerIndeterminateH;
        var contentW = 0f;
        var contentH = 0f;

        for (var stackIndex = 0; stackIndex < stack.Children.Count; stackIndex++)
        {
            var child = stack.Children[stackIndex];
            var measured = Measure(
                child,
                constraints.ForChild(childMaxW, childMaxH).DecidedByContent(stackIndetW, stackIndetH),
                ctx, ctx.ChildPath(path, stackIndex, child));
            result.Adopt(measured);
            if (PositionedOf(child, measured) is not null) continue;
            contentW = MathF.Max(contentW, measured.Bounds.Width);
            contentH = MathF.Max(contentH, measured.Bounds.Height);
        }

        var width = ResolveSelf(stack.Width, maxW, contentW);
        var height = ResolveSelf(stack.Height, maxH, contentH);
        result.Bounds = new Rect(0, 0, width, height);

        for (var i = 0; i < stack.Children.Count; i++)
        {
            var child = stack.Children[i];
            var measured = result.Children[i];
            var cw = measured.Bounds.Width;
            var ch = measured.Bounds.Height;
            var (alignX, alignY) = AlignOffset(stack.Align, width - cw, height - ch);

            if (PositionedOf(child, measured) is { } positioned)
            {
                var x = positioned.Start ?? (positioned.End is { } end ? width - cw - end : alignX);
                var y = positioned.Top ?? (positioned.Bottom is { } bottom ? height - ch - bottom : alignY);
                measured.Bounds = measured.Bounds with { X = x, Y = y };
            }
            else
            {
                measured.Bounds = measured.Bounds with { X = alignX, Y = alignY };
            }
        }

        // Spec S7 z-order: children paint (and hit-test, topmost-last) in Layer order — a stable
        // sort keeps declaration order for equal values (flow order = the painter's default).
        if (AnyLayered(result, stack))
        {
            var ordered = new List<(LayoutNode Node, int Z, int I)>(result.Children.Count);
            for (var i = 0; i < result.Children.Count; i++)
                ordered.Add((result.Children[i],
                    PositionedOf(stack.Children[i], result.Children[i]) is { } p ? p.Layer : 0, i));
            ordered.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.I.CompareTo(b.I));

            var sorted = new LayoutNode[ordered.Count];
            for (var i = 0; i < ordered.Count; i++) sorted[i] = ordered[i].Node;
            result.ReleaseChildren();
            result.AdoptAll(sorted);
        }

        return result;
    }

    /// <summary>Whether any child asked for a layer — the cheap question, asked before the sort that
    /// answering it would otherwise pay for on every Stack in the tree.</summary>
    private bool AnyLayered(LayoutNode result, Stack stack)
    {
        for (var i = 0; i < result.Children.Count; i++)
            if (PositionedOf(stack.Children[i], result.Children[i]) is { Layer: not 0 })
                return true;
        return false;
    }

    private LayoutNode MeasureScrollView(ScrollView scroll, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = ctx.Node(scroll);
        var horizontal = scroll.Axis == ScrollAxis.Horizontal;

        var child = Measure(scroll.Child, constraints.ForChild(
            horizontal ? float.PositiveInfinity : maxW,
            horizontal ? maxH : float.PositiveInfinity), ctx, ctx.ChildPath(path, 0));
        result.Adopt(child);

        var width = ResolveSelf(scroll.Width, maxW, MathF.Min(child.Bounds.Width, maxW));
        var height = ResolveSelf(scroll.Height, maxH, MathF.Min(child.Bounds.Height, maxH));
        result.Bounds = new Rect(0, 0, width, height);

        var maxOffset = MathF.Max(0, horizontal ? child.Bounds.Width - width : child.Bounds.Height - height);
        // Scroll compositor v1: the host's stored offset wins; the node's programmatic Offset is the
        // default until the user scrolls. The realizer registers the region via ScrollMeta.
        var offset = Math.Clamp(ctx.ScrollOffsets?.Get(path) ?? scroll.Offset, 0, maxOffset);
        ctx.ScrollMeta?.TryAdd(scroll, (path, maxOffset));
        child.Bounds = child.Bounds with
        {
            X = horizontal ? -offset : 0,
            Y = horizontal ? 0 : -offset,
        };

        // Spec S7 — Pinned PINNING (vertical v1): a Pinned at content-y y0 shows at y0 - offset;
        // once that would pass its own Offset from the viewport start, it pins there instead.
        if (!horizontal && offset > 0)
            PinSticky(child, accumulatedY: child.Bounds.Y);

        return result;
    }

    /// <summary>Walks the scrolled content for Pinned wrappers and clamps their viewport-relative Y
    /// (v1: vertical, no end-of-container release — that fence joins the compositor polish). Nested
    /// ScrollViews own their own pinning pass.</summary>
    private void PinSticky(LayoutNode node, float accumulatedY)
    {
        foreach (var child in node)
        {
            if (child.Source is ScrollView) continue;
            var viewportY = accumulatedY + child.Bounds.Y;
            if (child.Source is Pinned pinned && viewportY < pinned.Offset)
            {
                child.Bounds = child.Bounds with { Y = child.Bounds.Y + (pinned.Offset - viewportY) };
                continue; // the pinned subtree moves as one — no need to descend
            }
            PinSticky(child, accumulatedY + child.Bounds.Y);
        }
    }

    private LayoutNode MeasureBox(Box box, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
        var result = ctx.Node(box);
        var style = box.Style;

        // Spec S6, the Size channel: a FIXED width or height that changed glides to its new value
        // under the box's own Transition — measured at the interpolated size, so everything that
        // depends on it (the child's wrap, the siblings' share of the row) follows the glide too,
        // which is what `transition: width` does in a browser. Hug/Fill have no number to glide.
        if (ctx.Transitions is { } glide
            && TransitionStore.Only(style.Transition, StyleChannels.Size) is { } sizeSpec)
        {
            if (style.Width.Kind == SizeKind.Fixed)
                style = style with { Width = glide.Resolve(path + ":w", style.Width.Value, ctx.TimeMs, sizeSpec, ctx.ReducedMotion) };
            if (style.Height.Kind == SizeKind.Fixed)
                style = style with { Height = glide.Resolve(path + ":h", style.Height.Value, ctx.TimeMs, sizeSpec, ctx.ReducedMotion) };
        }

        // The indeterminate flags AS INHERITED — what the PARENT said about this axis, before this
        // box restates them for its own child below. A Fill on an axis the parent is sizing from
        // content has nothing to fill; the flex container has honoured that from the start, but a
        // BOX child read its available maximum anyway — which is how every option row of a hugging
        // Select panel came out the full width of the window.
        var inheritedIndeterminateW = constraints.Width.Indeterminate;
        var inheritedIndeterminateH = constraints.Height.Indeterminate;

        var maxWidthDp = CapDp(style.MaxWidth, ctx.WindowWidth);
        var maxHeightDp = CapDp(style.MaxHeight, ctx.WindowHeight);
        var selfMaxW = CapMax(maxW, maxWidthDp);
        var selfMaxH = CapMax(maxH, maxHeightDp);

        // Content box the child may use (explicit/Fill pin it; Hug passes the available through).
        var childMaxW = (style.Width.Kind == SizeKind.WindowMinus
            ? WindowSize(style.Width, ctx.WindowWidth)
            : ResolveForChild(style.Width, selfMaxW)) - style.Padding.Horizontal;
        var childMaxH = (style.Height.Kind == SizeKind.WindowMinus
            ? WindowSize(style.Height, ctx.WindowHeight)
            : ResolveForChild(style.Height, selfMaxH)) - style.Padding.Vertical;

        LayoutNode? child = null;
        if (box.Child is not null)
        {
            // The available space still bounds the child (text has to wrap somewhere), but on an
            // axis this box HUGS there is nothing to fill: say so, and a Fill child measures itself.

            // What the CHILD may fill against: Fixed decides the axis, Hug decides nothing — and
            // FILL passes the question through, because a Fill in a context that hands out no
            // width hands out none either. Treating Fill as determinate was the leak: an option
            // row's Fill box told ITS Fill row the width was known, the row took the viewport's
            // maximum, and every hugging ancestor up to the Select's panel followed it — one
            // dropdown as wide as the window.
            // A STRETCHED auto axis is determined after all: the parent already decided the size
            // this box will take (ResolveSelf below returns the available maximum), so the child
            // may fill it — this is what lets a page whose root box hugs still hand the window's
            // width to a Fill child, the way a CSS body does.
            var boxIndetW = style.Width.Kind switch
            {
                SizeKind.Fixed => false,
                SizeKind.Hug => !(stretchW != StretchKind.None && !inheritedIndeterminateW && !float.IsPositiveInfinity(selfMaxW)),
                _ => inheritedIndeterminateW,
            };
            var boxIndetH = style.Height.Kind switch
            {
                SizeKind.Fixed => false,
                SizeKind.Hug => !(stretchH != StretchKind.None && !inheritedIndeterminateH && !float.IsPositiveInfinity(selfMaxH)),
                _ => inheritedIndeterminateH,
            };
            // CSS block semantics on the WIDTH: a box whose width is determined stretches an
            // auto-sized child across it — a div's child div is full-width without asking. This is
            // what keeps a hugging Column inside a Fill card at the card's width on native, the way
            // the SAME tree already behaves on the web, where a Column realizes as width:auto.
            var boxStretchW = boxIndetW ? StretchKind.None : StretchKind.Block;
            // And on the HEIGHT, where CSS gives nothing and the author paid for it: a box with a
            // decided height hands that height to its child. A 56dp bar holding a Row got a Row as
            // tall as its tallest label, so `Cross = Center` centred inside THAT — and the toolbar
            // sat against the top of its own bar. The fix used to be `Height = Fill` on the child,
            // written by hand, in every bar, remembered every time.
            //
            // It reaches exactly what an auto-sized CONTAINER is: a Box, a Row, a Column. Text,
            // images and icons never took these flags (they size themselves), and a button, a link
            // or an input hugs, because a Block stretch stops at an inline-block — the same fence
            // the width has always respected.
            var boxStretchH = boxIndetH ? StretchKind.None : StretchKind.Block;
            child = Measure(
                box.Child,
                constraints.ForChild(MathF.Max(0, childMaxW), MathF.Max(0, childMaxH))
                    .DecidedByContent(boxIndetW, boxIndetH)
                    .Stretched(boxStretchW, boxStretchH),
                ctx, ctx.ChildPath(path, 0));
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Adopt(child);
        }

        var width = ResolveSelf(style.Width, selfMaxW, (child?.Bounds.Width ?? 0) + style.Padding.Horizontal, ctx.WindowWidth,
            indeterminate: inheritedIndeterminateW, stretched: stretchW != StretchKind.None);
        var height = ResolveSelf(style.Height, selfMaxH, (child?.Bounds.Height ?? 0) + style.Padding.Vertical, ctx.WindowHeight,
            indeterminate: inheritedIndeterminateH, stretched: stretchH != StretchKind.None);
        width = Clamp(width, style.MinWidth, maxWidthDp);
        height = Clamp(height, style.MinHeight, maxHeightDp);

        // Min/Max REFLOW (the CSS twin): when the clamp changed the box's extent after the child
        // was already measured, the child measured against a lie. Re-measure it against the final
        // size, which is now DETERMINATE on that axis — this is what lets a MatchAnchorWidth panel
        // hand its MinWidth down to the option rows instead of keeping them at their intrinsic
        // width inside a wider frame. Runs only when a Min/Max actually bit, so the common path
        // stays single-pass.
        if (child is not null
            && ((style.MinWidth > 0 || maxWidthDp >= 0)
                && MathF.Abs(width - style.Padding.Horizontal - child.Bounds.Width) > 0.5f
                || (style.MinHeight > 0 || maxHeightDp >= 0)
                && MathF.Abs(height - style.Padding.Vertical - child.Bounds.Height) > 0.5f))
        {
            var outerH2 = constraints.Height.Indeterminate;
            var clampedIndetH = style.Height.Kind == SizeKind.Hug
                && style.MinHeight <= 0 && maxHeightDp < 0 && outerH2;
            // The clamped width is a DECIDED size — block semantics again: the child stretches
            // across it, which is what centres a Stepper's reading inside its MinWidth cell.
            result.ReleaseChildren();
            child = Measure(
                box.Child!,
                constraints.ForChild(MathF.Max(0, width - style.Padding.Horizontal),
                        MathF.Max(0, height - style.Padding.Vertical))
                    .DecidedByContent(false, clampedIndetH)
                    .Stretched(StretchKind.Block, StretchKind.None),
                ctx, ctx.ChildPath(path, 0));
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Adopt(child);
        }

        // Spec S1 aspect-ratio (CSS twin): when exactly one axis is author-determined, the other
        // derives from it; two explicit axes win over the ratio (no constraint fight).
        if (style.AspectRatio > 0)
        {
            var widthSet = style.Width.Kind != SizeKind.Hug;
            var heightSet = style.Height.Kind != SizeKind.Hug;
            if (widthSet && !heightSet)
                height = Clamp(width / style.AspectRatio, style.MinHeight, maxHeightDp);
            else if (heightSet && !widthSet)
                width = Clamp(height * style.AspectRatio, style.MinWidth, maxWidthDp);
        }

        // A Fill child stretches to the resolved content box (its own measurement saw the max already;
        // pin the bounds so realizers paint the full extent).
        if (child?.Source is Box { Style.Width.Kind: SizeKind.Fill })
            child.Bounds = child.Bounds with { Width = MathF.Max(0, width - style.Padding.Horizontal) };
        if (child?.Source is Box { Style.Height.Kind: SizeKind.Fill })
            child.Bounds = child.Bounds with { Height = MathF.Max(0, height - style.Padding.Vertical) };

        result.Bounds = new Rect(0, 0, width, height);
        return result;
    }

    // ---- flex ------------------------------------------------------------------------------------

    /// <summary>
    /// Spec S4 — the grid track-sizing pass (CSS Grid twin, v1 auto-flow): Fixed tracks take their
    /// dp; Auto tracks size to their widest starting single-span item; Flex tracks share the
    /// remaining width by weight (collapsing to 0 in unbounded space). Children flow left→right,
    /// wrapping to a new row; a span clamps to the row's remainder. Rows size to their tallest cell.
    /// </summary>
    private LayoutNode MeasureGrid(Grid grid, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (maxW, maxH) = (constraints.MaxWidth, constraints.MaxHeight);
        var result = ctx.Node(grid);
        var columns = grid.Columns;
        var count = columns.Count;
        var rowGap = grid.RowGap ?? grid.Gap;
        var padH = grid.Padding.Horizontal;

        var avail = grid.Width.Kind == SizeKind.Fixed ? grid.Width.Value - padH
            : !float.IsPositiveInfinity(maxW) ? maxW - padH
            : float.PositiveInfinity;
        var gapTotal = grid.Gap * MathF.Max(0, count - 1);

        // Place children into (column, span) slots — auto-flow with span clamping.
        var placements = new (VisualNode Node, int Column, int Span, int Row)[grid.Children.Count];
        var col = 0;
        var row = 0;
        for (var i = 0; i < grid.Children.Count; i++)
        {
            var span = Math.Clamp(grid.Children[i].GridSpan < 1 ? 1 : grid.Children[i].GridSpan, 1, count);
            if (col + span > count) { col = 0; row++; }
            placements[i] = (grid.Children[i], col, span, row);
            col += span;
            if (col >= count) { col = 0; row++; }
        }

        // Track sizing: fixed → value; auto → widest starting single-span item; flex → weighted rest.
        var widths = new float[count];
        var flexTotal = 0f;
        var used = gapTotal;
        for (var c = 0; c < count; c++)
        {
            if (columns[c].Kind == SizeKind.Fixed) { widths[c] = columns[c].Value; used += widths[c]; }
            else if (columns[c].Kind == SizeKind.Fill) flexTotal += MathF.Max(0, columns[c].Value);
        }
        for (var c = 0; c < count; c++)
        {
            if (columns[c].Kind != SizeKind.Hug) continue;
            var widest = 0f;
            foreach (var pl in placements)
                if (pl.Column == c && pl.Span == 1)
                    widest = MathF.Max(widest, Measure(pl.Node, constraints.ForChild(float.PositiveInfinity, maxH), ctx, path + "/probe").Bounds.Width);
            widths[c] = widest;
            used += widest;
        }
        var leftover = float.IsPositiveInfinity(avail) ? 0 : MathF.Max(0, avail - used);
        for (var c = 0; c < count; c++)
            if (columns[c].Kind == SizeKind.Fill && flexTotal > 0)
                widths[c] = leftover * (MathF.Max(0, columns[c].Value) / flexTotal);

        // Measure each child at its cell width; rows size to the tallest cell.
        var rowCount = placements.Length > 0 ? placements[^1].Row + 1 : 0;
        var rowHeights = new float[rowCount];
        var laid = new LayoutNode[placements.Length];
        for (var i = 0; i < placements.Length; i++)
        {
            var (node, c, span, r) = placements[i];
            var cellW = grid.Gap * (span - 1);
            for (var k = c; k < c + span; k++) cellW += widths[k];
            var child = Measure(node, constraints.ForChild(cellW, maxH), ctx, ctx.ChildPath(path, i, node));
            // A Fill-width child pins to the cell (the realizer paints the full extent).
            if (CrossSizeKind(node, horizontal: false) == SizeKind.Fill || WidthKind(node) == SizeKind.Fill)
                child.Bounds = child.Bounds with { Width = cellW };
            laid[i] = child;
            rowHeights[r] = MathF.Max(rowHeights[r], child.Bounds.Height);
        }

        // Arrange.
        var xStarts = new float[count];
        var x = grid.Padding.Start;
        for (var c = 0; c < count; c++) { xStarts[c] = x; x += widths[c] + grid.Gap; }
        var y = grid.Padding.Top;
        for (var r = 0; r < rowCount; r++)
        {
            for (var i = 0; i < placements.Length; i++)
            {
                if (placements[i].Row != r) continue;
                laid[i].Bounds = laid[i].Bounds with { X = xStarts[placements[i].Column], Y = y };
                result.Adopt(laid[i]);
            }
            y += rowHeights[r] + rowGap;
        }

        var contentW = gapTotal + grid.Padding.Horizontal;
        foreach (var w in widths) contentW += w;
        var width = grid.Width.Kind switch
        {
            SizeKind.Fixed => grid.Width.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(maxW) => maxW,
            _ => contentW,
        };
        var height = grid.Height.Kind switch
        {
            SizeKind.Fixed => grid.Height.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(maxH) => maxH,
            _ => (rowCount > 0 ? y - rowGap : y) + grid.Padding.Bottom,
        };
        result.Bounds = new Rect(0, 0, width, height);
        return result;
    }
}
