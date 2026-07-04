using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>Everything a layout pass needs besides the tree: theme (type styles), text metrics, Dynamic Type factor.</summary>
public sealed class LayoutContext
{
    public LayoutContext(IAppTheme theme, ITextMeasurer measurer, float typeScale = 1f)
    {
        Theme = theme;
        Measurer = measurer;
        TypeScale = typeScale;
        Components = new ComponentContext(theme, typeScale);
    }

    public IAppTheme Theme { get; }
    public ITextMeasurer Measurer { get; }
    public float TypeScale { get; }

    /// <summary>The mode-free context handed to <see cref="UiComponent.Build"/> during expansion.</summary>
    public ComponentContext Components { get; }
}

/// <summary>A laid-out node: source, ABSOLUTE bounds (after the layout pass), children, text metrics.</summary>
public sealed class LayoutNode
{
    public LayoutNode(VisualNode source) => Source = source;

    public VisualNode Source { get; }
    public Rect Bounds { get; internal set; }
    public List<LayoutNode> Children { get; } = new();
    public TextMeasurement? Text { get; internal set; }
}

/// <summary>
/// The Photon flex layout engine (spec A2 — "own C# flex implementation"): single main axis, token
/// gaps that never collapse, <c>Flexible(n)</c> children sharing LEFTOVER space by weight, and the
/// truncation contract — TEXT children shrink to ellipsis before any sibling is pushed out; fixed
/// children never shrink. Size resolution per node: explicit &gt; Fill &gt; Hug (spec A1). The web
/// realizer does NOT use this engine (the browser lays out CSS flex); parity between the two is the
/// job of the cross-target layout harness (docs/SHARED-COMPONENTS-PLAN.md).
/// </summary>
public static class LayoutEngine
{
    public static LayoutNode Layout(VisualNode root, float viewportWidth, float viewportHeight, LayoutContext context)
    {
        var node = Measure(root, viewportWidth, viewportHeight, context);
        Absolutize(node, 0, 0);
        return node;
    }

    // ---- measurement (bounds are PARENT-RELATIVE until Absolutize) ------------------------------

    private static LayoutNode Measure(VisualNode node, float maxW, float maxH, LayoutContext ctx) => node switch
    {
        Box box => MeasureBox(box, maxW, maxH, ctx),
        FlexNode flex => MeasureFlex(flex, maxW, maxH, ctx),
        Text text => MeasureText(text, maxW, ctx),
        Pressable pressable => MeasureWrapper(pressable, pressable.Child, maxW, maxH, ctx),
        Flexible flexible => MeasureWrapper(flexible, flexible.Child, maxW, maxH, ctx),
        // A component expands INLINE: Build produces its subtree (pure, mode-free), which is measured
        // in place — the component wraps it in the layout tree, drawing nothing itself.
        UiComponent component => MeasureWrapper(component, component.Build(ctx.Components), maxW, maxH, ctx),
        Spacer => new LayoutNode(node), // zero outside a flex container (layout-only)
        _ => new LayoutNode(node),
    };

    private static LayoutNode MeasureWrapper(VisualNode node, VisualNode child, float maxW, float maxH, LayoutContext ctx)
    {
        var result = new LayoutNode(node);
        var inner = Measure(child, maxW, maxH, ctx);
        result.Children.Add(inner);
        result.Bounds = new Rect(0, 0, inner.Bounds.Width, inner.Bounds.Height);
        return result;
    }

    private static LayoutNode MeasureText(Text text, float maxW, LayoutContext ctx)
    {
        var result = new LayoutNode(text);
        var style = text.StyleOverride ?? ctx.Theme.Type(text.Role);
        var measurement = ctx.Measurer.Measure(text.Content, style, ctx.TypeScale, maxW, text.MaxLines);
        result.Text = measurement;
        result.Bounds = new Rect(0, 0, measurement.Width, measurement.Height);
        return result;
    }

    private static LayoutNode MeasureBox(Box box, float maxW, float maxH, LayoutContext ctx)
    {
        var result = new LayoutNode(box);
        var style = box.Style;

        var selfMaxW = CapMax(maxW, style.MaxWidth);
        var selfMaxH = CapMax(maxH, style.MaxHeight);

        // Content box the child may use (explicit/Fill pin it; Hug passes the available through).
        var childMaxW = ResolveForChild(style.Width, selfMaxW) - style.Padding.Horizontal;
        var childMaxH = ResolveForChild(style.Height, selfMaxH) - style.Padding.Vertical;

        LayoutNode? child = null;
        if (box.Child is not null)
        {
            child = Measure(box.Child, MathF.Max(0, childMaxW), MathF.Max(0, childMaxH), ctx);
            child.Bounds = child.Bounds with { X = style.Padding.Start, Y = style.Padding.Top };
            result.Children.Add(child);
        }

        var width = ResolveSelf(style.Width, selfMaxW, (child?.Bounds.Width ?? 0) + style.Padding.Horizontal);
        var height = ResolveSelf(style.Height, selfMaxH, (child?.Bounds.Height ?? 0) + style.Padding.Vertical);
        width = Clamp(width, style.MinWidth, style.MaxWidth);
        height = Clamp(height, style.MinHeight, style.MaxHeight);

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

    private static LayoutNode MeasureFlex(FlexNode flex, float maxW, float maxH, LayoutContext ctx)
    {
        var result = new LayoutNode(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (maxW, maxH) : (maxH, maxW);
        var mainSize = horizontal ? flex.Width : flex.Height;
        var crossSize = horizontal ? flex.Height : flex.Width;
        var padMain = horizontal ? flex.Padding.Horizontal : flex.Padding.Vertical;
        var padCross = horizontal ? flex.Padding.Vertical : flex.Padding.Horizontal;

        var mainAvail = mainSize.Kind == SizeKind.Fixed ? mainSize.Value - padMain
            : !float.IsPositiveInfinity(mainMax) ? mainMax - padMain
            : float.PositiveInfinity;

        // "Leftover" for Flexible/Spacer children exists when the main extent is pinned (Fixed, or Fill
        // in bounded space) — AND when a Hug container holding flexible children sits in FINITE space:
        // flexibles declare the intent to fill, so the container takes the available extent (CSS parity —
        // a stretched row with a flex-grow child distributes over the stretched width). Flexibles
        // collapse to 0 only in genuinely unbounded space (e.g. inside scroll content), and Spacers
        // additionally "lose to content" when space is tight (leftover floors at 0).
        var hasFlexibles = false;
        foreach (var c in flex.Children)
            if (c is Flexible or Spacer { Flex: > 0 }) { hasFlexibles = true; break; }
        var mainBounded = mainSize.Kind == SizeKind.Fixed
                          || (!float.IsPositiveInfinity(mainAvail)
                              && (mainSize.Kind == SizeKind.Fill || hasFlexibles));

        var crossAvail = crossSize.Kind == SizeKind.Fixed ? crossSize.Value - padCross
            : !float.IsPositiveInfinity(crossMax) ? crossMax - padCross
            : float.PositiveInfinity;

        var children = flex.Children;
        var laid = new LayoutNode?[children.Count];
        var mains = new float[children.Count];
        var flexWeights = new int[children.Count];
        var gapTotal = flex.Gap * MathF.Max(0, children.Count - 1);

        // Pass 1 — rigid children (flexibles deferred; text measured at full availability first).
        var rigidSum = 0f;
        for (var i = 0; i < children.Count; i++)
        {
            switch (children[i])
            {
                case Flexible f:
                    flexWeights[i] = f.Flex;
                    continue;
                case Spacer { Flex: > 0 } s:
                    flexWeights[i] = s.Flex;
                    continue;
                case Spacer fixedSpacer:
                    mains[i] = fixedSpacer.FixedLength;
                    rigidSum += mains[i];
                    continue;
            }

            var childMaxW = horizontal ? mainAvail : crossAvail;
            var childMaxH = horizontal ? crossAvail : mainAvail;
            var child = Measure(children[i], childMaxW, childMaxH, ctx);
            laid[i] = child;
            mains[i] = horizontal ? child.Bounds.Width : child.Bounds.Height;
            rigidSum += mains[i];
        }

        // Truncation contract (spec A2): on overflow, TEXT children shrink to ellipsis before any
        // sibling is pushed out; fixed children never shrink. Applies whenever the available extent is
        // finite — a Hug row inside a bounded parent must not overflow it either.
        if (!float.IsPositiveInfinity(mainAvail) && rigidSum + gapTotal > mainAvail && horizontal)
        {
            var deficit = rigidSum + gapTotal - mainAvail;
            var textTotal = 0f;
            for (var i = 0; i < children.Count; i++)
                if (children[i] is Text) textTotal += mains[i];

            if (textTotal > 0)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i] is not Text text) continue;
                    var reduced = MathF.Max(0, mains[i] - deficit * (mains[i] / textTotal));
                    var style = text.StyleOverride ?? ctx.Theme.Type(text.Role);
                    var remeasured = ctx.Measurer.Measure(text.Content, style, ctx.TypeScale, reduced,
                        Math.Max(1, text.MaxLines));
                    var node = new LayoutNode(text) { Text = remeasured };
                    node.Bounds = new Rect(0, 0, remeasured.Width, remeasured.Height);
                    laid[i] = node;
                    rigidSum -= mains[i] - (horizontal ? node.Bounds.Width : node.Bounds.Height);
                    mains[i] = horizontal ? node.Bounds.Width : node.Bounds.Height;
                }
            }
        }

        // Pass 2 — distribute leftover to flexible children by weight.
        var flexTotal = 0;
        foreach (var w in flexWeights) flexTotal += w;
        var leftover = mainBounded ? MathF.Max(0, mainAvail - rigidSum - gapTotal) : 0f;

        for (var i = 0; i < children.Count; i++)
        {
            if (flexWeights[i] == 0) continue;
            var share = flexTotal > 0 ? leftover * flexWeights[i] / flexTotal : 0f;
            mains[i] = share;

            if (children[i] is Flexible flexible)
            {
                var childMaxW = horizontal ? share : crossAvail;
                var childMaxH = horizontal ? crossAvail : share;
                var child = Measure(flexible.Child, childMaxW, childMaxH, ctx);
                // The flexible slot IS the share on the main axis (the child fills it).
                child.Bounds = horizontal
                    ? child.Bounds with { Width = share }
                    : child.Bounds with { Height = share };
                var wrapper = new LayoutNode(flexible) { Bounds = child.Bounds };
                wrapper.Children.Add(child);
                laid[i] = wrapper;
            }
            else
            {
                laid[i] = new LayoutNode(children[i]); // flexible Spacer: pure space
            }
        }

        // Container size.
        var contentMain = rigidSum + (flexTotal > 0 ? leftover : 0) + gapTotal;
        var main = mainSize.Kind switch
        {
            SizeKind.Fixed => mainSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(mainMax) => mainMax,
            _ => contentMain + padMain,
        };

        var crossContent = 0f;
        foreach (var child in laid)
            if (child is not null)
                crossContent = MathF.Max(crossContent, horizontal ? child.Bounds.Height : child.Bounds.Width);
        var cross = crossSize.Kind switch
        {
            SizeKind.Fixed => crossSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(crossMax) => crossMax,
            _ => crossContent + padCross,
        };

        // Pass 3 — arrange along main (alignment applies when no flexible consumed the leftover).
        var free = MathF.Max(0, (main - padMain) - contentMain);
        var cursor = (horizontal ? flex.Padding.Start : flex.Padding.Top) + flex.Main switch
        {
            MainAlign.Center => free / 2,
            MainAlign.End => free,
            _ => 0,
        };
        var betweenExtra = flex.Main == MainAlign.SpaceBetween && children.Count > 1 ? free / (children.Count - 1) : 0;

        var crossExtent = cross - padCross;
        for (var i = 0; i < children.Count; i++)
        {
            var child = laid[i];
            if (child is null)
            {
                // Pure space (Spacer): no layout node, but its extent still advances the cursor.
                cursor += mains[i] + flex.Gap + betweenExtra;
                continue;
            }

            var childCross = horizontal ? child.Bounds.Height : child.Bounds.Width;
            var crossPos = (horizontal ? flex.Padding.Top : flex.Padding.Start) + flex.Cross switch
            {
                CrossAlign.Center => (crossExtent - childCross) / 2,
                CrossAlign.End => crossExtent - childCross,
                _ => 0,
            };
            if (flex.Cross == CrossAlign.Stretch && children[i] is not Text
                && CrossSizeKind(children[i], horizontal) != SizeKind.Fixed)
            {
                // CSS parity: stretch fills AUTO cross sizes only — an explicit cross size is kept.
                childCross = crossExtent;
                child.Bounds = horizontal
                    ? child.Bounds with { Height = crossExtent }
                    : child.Bounds with { Width = crossExtent };
            }

            child.Bounds = horizontal
                ? child.Bounds with { X = cursor, Y = crossPos }
                : child.Bounds with { X = crossPos, Y = cursor };
            result.Children.Add(child);
            cursor += mains[i] + flex.Gap + betweenExtra;
        }

        result.Bounds = horizontal ? new Rect(0, 0, main, cross) : new Rect(0, 0, cross, main);
        return result;
    }

    // ---- helpers ----------------------------------------------------------------------------------

    /// <summary>The child's declared size KIND on the flex cross axis (wrappers look through to their
    /// content; components can't be known without building — treated as Hug, i.e. stretchable).</summary>
    private static SizeKind CrossSizeKind(VisualNode node, bool horizontal) => node switch
    {
        Box box => (horizontal ? box.Style.Height : box.Style.Width).Kind,
        FlexNode flex => (horizontal ? flex.Height : flex.Width).Kind,
        Pressable pressable => CrossSizeKind(pressable.Child, horizontal),
        Flexible flexible => CrossSizeKind(flexible.Child, horizontal),
        _ => SizeKind.Hug,
    };

    private static float CapMax(float available, float styleMax) =>
        styleMax > 0 ? MathF.Min(available, styleMax) : available;

    /// <summary>Max extent a child may use, given the node's own size request.</summary>
    private static float ResolveForChild(SizeValue size, float available) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        _ => available,
    };

    /// <summary>Own size: explicit &gt; Fill &gt; Hug (spec A1).</summary>
    private static float ResolveSelf(SizeValue size, float available, float hug) => size.Kind switch
    {
        SizeKind.Fixed => size.Value,
        SizeKind.Fill when !float.IsPositiveInfinity(available) => available,
        _ => hug,
    };

    private static float Clamp(float value, float min, float max)
    {
        if (min > 0) value = MathF.Max(value, min);
        if (max > 0) value = MathF.Min(value, max);
        return value;
    }

    private static void Absolutize(LayoutNode node, float originX, float originY)
    {
        node.Bounds = node.Bounds with { X = node.Bounds.X + originX, Y = node.Bounds.Y + originY };
        foreach (var child in node.Children)
            Absolutize(child, node.Bounds.X, node.Bounds.Y);
    }
}
