using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The flex algorithm, which is long enough to be its own reading: one pass for a single
/// line and one for a wrapped run of them.</summary>
internal sealed partial class MeasureVisitor
{
    private LayoutNode MeasureFlex(FlexNode flex, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var (stretchW, stretchH) = (constraints.Width.Stretch, constraints.Height.Stretch);
        if (flex.Wrap) return MeasureFlexWrapped(flex, constraints, ctx, path);

        var result = ctx.Node(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (constraints.MaxWidth, constraints.MaxHeight) : (constraints.MaxHeight, constraints.MaxWidth);
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
        // additionally "lose to content" when space is tight (leftover floors at 0). A Flexible of
        // weight ZERO declares no such intent: it takes no share, so it is a rigid item (#680).
        var hasFlexibles = false;
        foreach (var c in flex)
            if (c is Flexible { Flex: > 0 } or Spacer { Flex: > 0 }) { hasFlexibles = true; break; }
        // On an INDETERMINATE main axis the available maximum is not a size anyone granted — it is
        // the measuring parent's upper bound. Distributing leftover against it made a Flexible
        // spacer swallow the viewport: an option row's Fill (inherited-indeterminate) inside a
        // hugging Select panel measured its spacer against 1180dp of "available" and dragged every
        // hugging ancestor to the width of the window.
        var mainIndeterminateNow = horizontal ? constraints.Width.Indeterminate : constraints.Height.Indeterminate;
        var mainBounded = mainSize.Kind == SizeKind.Fixed
                          || (!float.IsPositiveInfinity(mainAvail)
                              && !mainIndeterminateNow
                              && (mainSize.Kind == SizeKind.Fill || hasFlexibles));

        var crossAvail = crossSize.Kind == SizeKind.Fixed ? crossSize.Value - padCross
            : !float.IsPositiveInfinity(crossMax) ? crossMax - padCross
            : float.PositiveInfinity;

        // The flags this container hands its CHILDREN — the same restatement MeasureBox makes, on
        // flex axes: Fixed decides the axis, Hug decides nothing (unless the parent stretched this
        // container, which makes the auto size a real one), and Fill passes the question through.
        // Without this a row with a FIXED width told its children nothing (they inherited whatever
        // the context said), and a hugging row told them the viewport was theirs to fill.
        var crossIndeterminateNow = horizontal ? constraints.Height.Indeterminate : constraints.Width.Indeterminate;
        var mainStretchedIn = horizontal ? stretchW : stretchH;
        var crossStretchedIn = horizontal ? stretchH : stretchW;
        var childIndetMain = mainSize.Kind switch
        {
            SizeKind.Fixed => false,
            SizeKind.Hug => !(mainStretchedIn != StretchKind.None && !mainIndeterminateNow && !float.IsPositiveInfinity(mainMax)),
            _ => mainIndeterminateNow,
        };
        var childIndetCross = crossSize.Kind switch
        {
            SizeKind.Fixed => false,
            SizeKind.Hug => !(crossStretchedIn != StretchKind.None && !crossIndeterminateNow && !float.IsPositiveInfinity(crossMax)),
            _ => crossIndeterminateNow,
        };
        var (childIndetW, childIndetH) = horizontal ? (childIndetMain, childIndetCross) : (childIndetCross, childIndetMain);

        // Whether THIS container decides the child's cross size for it. Only when the container's
        // own cross extent is known: a hugging container has nothing to hand out, and CSS agrees —
        // stretch there means "as wide as the widest sibling", which is a second pass, not a size.
        bool StretchesCross(VisualNode child)
        {
            if ((child.AlignSelf ?? flex.Cross) != CrossAlign.Stretch) return false;
            if (float.IsPositiveInfinity(crossAvail)) return false;
            return !childIndetCross;
        }

        // The CROSS stretch this container grants a child, answered rather than written onto a
        // context for `Measure` to pick up on the way past.
        (StretchKind W, StretchKind H) CrossStretch(VisualNode child) =>
            !StretchesCross(child) ? (StretchKind.None, StretchKind.None)
            : horizontal ? (StretchKind.None, StretchKind.Flex)
            : (StretchKind.Flex, StretchKind.None);

        // Every child measures under the flags THIS container restated. A flexible's share is a
        // size the container genuinely granted, so the main axis is determinate inside the slot
        // regardless of how the container itself is sized. A zero weight without a basis is the
        // opposite case: its main size is its content's, so that axis is decided by the content.
        LayoutNode MeasureChild(VisualNode child, float w, float h, string childPath,
            bool mainGranted = false, StretchKind stretchW = StretchKind.None,
            StretchKind stretchH = StretchKind.None, bool truncating = false, bool contentMain = false,
            bool ceiling = false)
        {
            var forChild = constraints.ForChild(w, h)
                .DecidedByContent(
                    (childIndetW && !(mainGranted && horizontal)) || (contentMain && horizontal),
                    (childIndetH && !(mainGranted && !horizontal)) || (contentMain && !horizontal))
                .Stretched(stretchW, stretchH) with { WidthIsACeiling = ceiling };
            return Measure(child, truncating ? forChild.Truncated() : forChild, ctx, childPath);
        }

        var children = flex.Children;
        var laid = new LayoutNode?[children.Count];
        var mains = new float[children.Count];
        var flexWeights = new float[children.Count];
        var gapTotal = flex.Gap * MathF.Max(0, children.Count - 1);

        // The SLOT a Flexible occupies: its child laid out in a main extent this row granted — a
        // weight's share of the leftover, a zero weight's basis, or what an overflowing line left
        // it — and the wrapper pinned to it.
        LayoutNode Slot(Flexible flexible, int i, float main, bool truncating = false)
        {
            // A Flexible is layout-transparent: whatever the container would stretch, it
            // stretches THROUGH it. Without this the wrapper grew to the cell and the content
            // inside it stayed at its own width, which is exactly what the tab labels did.
            var (fsW, fsH) = CrossStretch(flexible);
            var at = ctx.ChildPath(ctx.ChildPath(path, i, flexible), 0);
            // The extent IS the slot's main size (the item is pinned to it below), so the child is
            // stretched on the main axis too, on top of whatever the cross axis granted: an
            // auto-sized cell takes the extent and lays out inside it — a flex item's autos fill
            // the cell, per CSS.
            LayoutNode Laid(bool ceiling) => MeasureChild(flexible.Child, horizontal ? main : crossAvail,
                horizontal ? crossAvail : main, at, mainGranted: true,
                stretchW: horizontal ? StretchKind.Flex : fsW,
                stretchH: horizontal ? fsH : StretchKind.Flex, truncating: truncating, ceiling: ceiling);
            var child = Laid(ceiling: false);
            // A scroller's width is a ceiling in its item (the web's `max-width: 100%`): one that came
            // out wider than the extent is measured again under it, so it draws and scrolls at the
            // capped width. Asked of what was measured, which for an AdaptiveNode is its arm.
            if (MainSizeIsACeiling(SizedBy(child).Source, horizontal)
                && (horizontal ? child.Bounds.Width : child.Bounds.Height) > main + 0.01f)
                child = Laid(ceiling: true);
            return FlexItem(flexible, child, main, horizontal, ctx);
        }

        // A RIGID item measured again inside the extent an overflowing line leaves it. A zero
        // weight goes into a slot of that extent, with a basis or without one: measured through its
        // wrapper it would hug its content, so a child wider than the extent would hold the item at
        // the child's width, where the web shrinks the item (`min-width: 0`) and lets the child
        // overflow it.
        LayoutNode Remeasure(int i, float main, bool truncating = false)
        {
            if (children[i] is Flexible zero && flexWeights[i] == 0)
                return Slot(zero, i, main, truncating);
            var (sW, sH) = CrossStretch(children[i]);
            return MeasureChild(children[i], horizontal ? main : crossAvail,
                horizontal ? crossAvail : main, ctx.ChildPath(path, i, children[i]),
                stretchW: sW, stretchH: sH, truncating: truncating);
        }

        // Pass 1 — rigid children (flexibles deferred; text measured at full availability first).
        var rigidSum = 0f;
        for (var i = 0; i < children.Count; i++)
        {
            switch (children[i])
            {
                case Flexible f:
                    // Spec B14: an AnimateChanges weight LAYS OUT at the animator's interpolated
                    // value — forward changes glide over Motion.Base, everything else snaps.
                    flexWeights[i] = ctx.Transitions?.Resolve(ctx.ChildPath(path, i, f), f.Flex, ctx.TimeMs,
                        f.AnimateChanges, ctx.ReducedMotion) ?? f.Flex;
                    if (flexWeights[i] > 0) continue;
                    // A ZERO weight takes no share (#680) — Flutter's inflexible child, CSS's
                    // `flex-grow: 0` — so it is a rigid item, laid out here. Deferred with the
                    // weighted ones, it vanished: pass 2 skips a weight of zero, and nothing else
                    // laid it out. With a basis it sits in a slot of exactly that size; without one
                    // it starts from its CONTENT (`flex-basis: auto`), so the main axis is decided
                    // by what goes in it and a Fill inside has nothing to fill — as in a browser,
                    // and as Flutter, which lays an inflexible child out unbounded, refuses it.
                    flexWeights[i] = 0;
                    LayoutNode inflexible;
                    if (f.Basis > 0)
                    {
                        inflexible = Slot(f, i, f.Basis);
                    }
                    else
                    {
                        var (zsW, zsH) = CrossStretch(f);
                        inflexible = MeasureChild(f, horizontal ? mainAvail : crossAvail,
                            horizontal ? crossAvail : mainAvail, ctx.ChildPath(path, i, f),
                            stretchW: zsW, stretchH: zsH, contentMain: true);
                    }
                    laid[i] = inflexible;
                    mains[i] = horizontal ? inflexible.Bounds.Width : inflexible.Bounds.Height;
                    rigidSum += mains[i];
                    continue;
                case Spacer { Flex: > 0 } s:
                    flexWeights[i] = ctx.Transitions?.Resolve(ctx.ChildPath(path, i, s), s.Flex, ctx.TimeMs,
                        s.AnimateChanges, ctx.ReducedMotion) ?? s.Flex;
                    continue;
                case Spacer fixedSpacer:
                    mains[i] = fixedSpacer.FixedLength;
                    rigidSum += mains[i];
                    continue;
            }

            var childMaxW = horizontal ? mainAvail : crossAvail;
            var childMaxH = horizontal ? crossAvail : mainAvail;
            var (csW, csH) = CrossStretch(children[i]);
            var child = MeasureChild(children[i], childMaxW, childMaxH, ctx.ChildPath(path, i, children[i]),
                stretchW: csW, stretchH: csH);
            laid[i] = child;
            mains[i] = horizontal ? child.Bounds.Width : child.Bounds.Height;
            rigidSum += mains[i];
        }

        // Truncation contract (spec A2): on overflow, TEXT children shrink to ellipsis before any
        // sibling is pushed out; fixed children never shrink. Applies whenever the available extent is
        // finite — a Hug row inside a bounded parent must not overflow it either. ROWS ONLY, for every
        // item: a single-line column takes nothing back from an overflow here yet, so a zero weight
        // at a basis of 540 after a fixed 100, in a column 400 tall, stays 540 and runs 240 past the
        // column's end, where a browser shrinks it to 300 (`min-height: 0`).
        if (!float.IsPositiveInfinity(mainAvail) && rigidSum + gapTotal > mainAvail && horizontal)
        {
            // A TEXT CHILD, seen through layout-transparent wrappers. Asking `is Text` here is what
            // made `Pressable(Text(…))` run past the end of a fixed row: not a text, so not cut, so
            // its floor was its longest word and nothing could shrink it. A ZERO weight is not cut
            // here, whatever it holds: the web writes it `flex: 0 <shrink> <basis>; min-width: 0`,
            // an item that gives space back by its shrink TOGETHER with the other items that
            // shrink, so it gives it in the flex-shrink pass below, and never first, as text.
            bool Cuttable(int i) =>
                TextWithin(children[i]) is not null && children[i] is not Flexible { Flex: 0 };

            var deficit = rigidSum + gapTotal - mainAvail;
            var textTotal = 0f;
            for (var i = 0; i < children.Count; i++)
                if (Cuttable(i)) textTotal += mains[i];

            if (textTotal > 0)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    if (!Cuttable(i)) continue;
                    var reduced = MathF.Max(0, mains[i] - deficit * (mains[i] / textTotal));
                    // The cut is a RE-MEASURE of the item, through the same pass everything else
                    // takes, carrying the line cap on the constraints. It used to be built here by
                    // hand from `ctx.Measurer` — which could only ever cut a bare Text, dropped a
                    // rich text's runs on the floor by rebuilding the node from PlainContent, and
                    // had already once measured against a different face than the one drawn.
                    var recut = Remeasure(i, reduced, truncating: true);
                    laid[i] = recut;
                    rigidSum -= mains[i] - (horizontal ? recut.Bounds.Width : recut.Bounds.Height);
                    mains[i] = horizontal ? recut.Bounds.Width : recut.Bounds.Height;
                }
            }

            // FLEX-SHRINK (the CSS twin, and the rest of the same contract): text is asked first
            // because ellipsis is the cheapest loss, but if the line STILL does not fit, every
            // item that is not pinned gives up a proportional share and re-measures inside it.
            // Without this a row of auto-sized cells simply ran off the right edge and the clip
            // ate it — silently, because a clipped press region cannot be pressed either.
            deficit = rigidSum + gapTotal - mainAvail;
            if (deficit > 0.5f)
            {
                // What each item may give, and how hard it is asked. An item that is not a zero
                // weight stops at its min-content floor (`min-width: auto` on the web) and is asked
                // in proportion to the ROOM it has above it: a floor one item refuses to cross is
                // width the others have to give, which is what makes a hugging button keep its word
                // while the stretchy one beside it absorbs the overflow. A ZERO weight is written
                // `flex: 0 <shrink> <basis>; min-width: 0`, so its floor is zero, a child wider than
                // the item no longer holds it up (the web lets the child overflow it), and it is
                // asked by its shrink times its size, as CSS scales a shrink factor: `shrink: 3`
                // gives three times what `shrink: 1` of the same size gives.
                var floors = new float[children.Count];
                var weights = new float[children.Count];
                var yielding = 0f;
                for (var i = 0; i < children.Count; i++)
                {
                    if (!Shrinkable(children[i])) continue;
                    if (children[i] is Flexible { Flex: 0 } zero)
                    {
                        weights[i] = zero.Shrink * mains[i];
                    }
                    else
                    {
                        floors[i] = MathF.Min(mains[i], MinContentWidth(children[i], ctx));
                        weights[i] = mains[i] - floors[i];
                    }
                    yielding += mains[i] - floors[i];
                }

                var taking = MathF.Min(deficit, yielding);
                if (taking > 0)
                {
                    // CSS's loop (Flexbox §9.7): what is taken is shared by weight; an item whose
                    // share would carry it past its floor stops AT the floor, and what it could not
                    // give is shared again among the rest. A share weighted by room never passes a
                    // floor, so a line without a zero weight settles in the first round, on the
                    // numbers it always had; only a zero weight's share is ever cut short.
                    var given = new float[children.Count];
                    var settled = new bool[children.Count];
                    var remaining = taking;
                    while (remaining > 0.01f)
                    {
                        var weightSum = 0f;
                        for (var i = 0; i < children.Count; i++)
                            if (!settled[i] && weights[i] > 0) weightSum += weights[i];
                        if (weightSum <= 0) break;

                        var stopping = 0f;
                        for (var i = 0; i < children.Count; i++)
                        {
                            if (settled[i] || weights[i] <= 0) continue;
                            if (remaining * (weights[i] / weightSum) <= mains[i] - floors[i]) continue;
                            given[i] = mains[i] - floors[i];
                            stopping += given[i];
                            settled[i] = true;
                        }
                        if (stopping > 0)
                        {
                            remaining -= stopping;
                            continue;
                        }

                        for (var i = 0; i < children.Count; i++)
                            if (!settled[i] && weights[i] > 0) given[i] = remaining * (weights[i] / weightSum);
                        break;
                    }

                    for (var i = 0; i < children.Count; i++)
                    {
                        if (given[i] <= 0) continue;
                        var bound = MathF.Max(floors[i], mains[i] - given[i]);
                        var reflowed = Remeasure(i, bound);
                        laid[i] = reflowed;
                        var shrunk = horizontal ? reflowed.Bounds.Width : reflowed.Bounds.Height;
                        rigidSum -= mains[i] - shrunk;
                        mains[i] = shrunk;
                    }
                }
            }
        }

        // Pass 2 — distribute leftover to flexible children by weight.
        var flexTotal = 0f;
        foreach (var w in flexWeights) flexTotal += w;
        var leftover = mainBounded ? MathF.Max(0, mainAvail - rigidSum - gapTotal) : 0f;

        for (var i = 0; i < children.Count; i++)
        {
            if (flexWeights[i] == 0) continue;

            // Max-content sizing (the CSS twin): when there is no leftover to distribute — the
            // main axis is indeterminate or unbounded — a Flexible with CONTENT contributes its
            // own intrinsic size, exactly what a flex-grow item contributes to a max-content
            // container. A share of 0 here erased real content: a drawer's list rows measured
            // 0x0 because their row was hugging. (A flexible SPACER stays 0 — pure space.)
            if (!mainBounded && children[i] is Flexible unbounded)
            {
                var (usW, usH) = CrossStretch(unbounded);
                var intrinsic = MeasureChild(unbounded.Child, horizontal ? mainAvail : crossAvail,
                    horizontal ? crossAvail : mainAvail, ctx.ChildPath(ctx.ChildPath(path, i, unbounded), 0),
                    stretchW: usW, stretchH: usH);
                mains[i] = horizontal ? intrinsic.Bounds.Width : intrinsic.Bounds.Height;
                rigidSum += mains[i];
                var grown = ctx.Node(unbounded, intrinsic.Bounds);
                grown.Adopt(intrinsic);
                laid[i] = grown;
                continue;
            }

            var share = flexTotal > 0 ? leftover * flexWeights[i] / flexTotal : 0f;
            mains[i] = share;

            if (children[i] is Flexible flexible)
            {
                // The flexible slot IS the share on the main axis (the child fills it).
                laid[i] = Slot(flexible, i, share);
            }
            else
            {
                laid[i] = ctx.Node(children[i]); // flexible Spacer: pure space
            }
        }

        // Container size. Fill on an axis the PARENT is sizing from its content has nothing to fill
        // — see AxisConstraint.Indeterminate. Without this a `Centered()` wrapper (Fill on both
        // axes, which is what makes centring possible at all) dragged its hugging parent to the
        // full width of the window: a 16dp badge came out 600dp wide, shoving its neighbours off.
        var mainIndeterminate = horizontal ? constraints.Width.Indeterminate : constraints.Height.Indeterminate;
        var crossIndeterminate = horizontal ? constraints.Height.Indeterminate : constraints.Width.Indeterminate;

        var mainStretched = horizontal ? stretchW : stretchH;
        var crossStretched = horizontal ? stretchH : stretchW;

        var contentMain = rigidSum + (flexTotal > 0 ? leftover : 0) + gapTotal;
        var main = mainSize.Kind switch
        {
            SizeKind.Fixed => mainSize.Value,
            SizeKind.Fill when !mainIndeterminate && !float.IsPositiveInfinity(mainMax) => mainMax,
            // Stretched by the parent: the auto size IS the size it was given, and only then does
            // MainAlign have room to place anything — this is what makes a centred label centre.
            SizeKind.Hug when mainStretched != StretchKind.None && !mainIndeterminate && !float.IsPositiveInfinity(mainMax) => mainMax,
            _ => contentMain + padMain,
        };

        var crossContent = 0f;
        // Indexed to children.Count: the rented array is LONGER than the child list, and the slots
        // past it belong to whoever borrowed it last.
        for (var ci = 0; ci < children.Count; ci++)
            if (laid[ci] is { } laidChild)
                crossContent = MathF.Max(crossContent, horizontal ? laidChild.Bounds.Height : laidChild.Bounds.Width);
        var cross = crossSize.Kind switch
        {
            SizeKind.Fixed => crossSize.Value,
            SizeKind.Fill when !crossIndeterminate && !float.IsPositiveInfinity(crossMax) => crossMax,
            SizeKind.Hug when crossStretched != StretchKind.None && !crossIndeterminate && !float.IsPositiveInfinity(crossMax) => crossMax,
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
            // Spec S1 align-self: a child may override the container's Cross for itself (CSS twin).
            var alignment = children[i].AlignSelf ?? flex.Cross;
            var crossPos = (horizontal ? flex.Padding.Top : flex.Padding.Start) + alignment switch
            {
                CrossAlign.Center => (crossExtent - childCross) / 2,
                CrossAlign.End => crossExtent - childCross,
                _ => 0,
            };
            if (alignment == CrossAlign.Stretch
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
            result.Adopt(child);
            cursor += mains[i] + flex.Gap + betweenExtra;
        }

        result.Bounds = horizontal ? new Rect(0, 0, main, cross) : new Rect(0, 0, cross, main);
        return result;
    }

    /// <summary>
    /// Spec S3 — the wrapping flex pass (the CSS flex-wrap twin), as it stands. A child breaks onto a
    /// new line when the size it asks for would overflow the main extent: a Flexible's basis when it
    /// declares one, and the size the child measures otherwise. That is CSS's hypothetical size for
    /// every child but one: a WEIGHTED Flexible without a basis starts from zero in CSS
    /// (<c>flex: n 1 0%</c>), and this pass breaks it at its child's natural size, so two of them
    /// that share a line in a browser can take a line each here (#728). Each line is then resolved
    /// on its own (below) and arranged with the container's <see cref="FlexNode.Main"/>; within its
    /// line a child follows <see cref="FlexNode.Cross"/> (or its own AlignSelf); lines stack with
    /// RunGap.
    /// </summary>
    private LayoutNode MeasureFlexWrapped(FlexNode flex, LayoutConstraints constraints, LayoutContext ctx, string path)
    {
        var result = ctx.Node(flex);
        var horizontal = flex is Row;

        var (mainMax, crossMax) = horizontal ? (constraints.MaxWidth, constraints.MaxHeight) : (constraints.MaxHeight, constraints.MaxWidth);
        var mainSize = horizontal ? flex.Width : flex.Height;
        var crossSize = horizontal ? flex.Height : flex.Width;
        var padMain = horizontal ? flex.Padding.Horizontal : flex.Padding.Vertical;
        var padCross = horizontal ? flex.Padding.Vertical : flex.Padding.Horizontal;
        var runGap = flex.RunGap ?? flex.Gap;

        var mainAvail = mainSize.Kind == SizeKind.Fixed ? mainSize.Value - padMain
            : !float.IsPositiveInfinity(mainMax) ? mainMax - padMain
            : float.PositiveInfinity;

        // Measure every child at the size the line breaker works from: its basis when it declares
        // one, its natural size otherwise. With a basis that is CSS's hypothetical size exactly: a
        // pane with a basis of 440 asks for 440 whatever its content happens to measure, so two of
        // them share a line while there is room for both and take a line each when there is not.
        // Without one it is CSS's for every child but a WEIGHTED Flexible, which CSS starts from
        // zero (`flex: n 1 0%`) and this starts from its child's natural size, the v1 behaviour in
        // which a Flexible simply degraded to its child (#728).
        var measured = new List<LayoutNode>(flex.Children.Count);
        var sources = new List<VisualNode>(flex.Children.Count);
        var hypothetical = new List<float>(flex.Children.Count);
        var grow = new List<int>(flex.Children.Count);
        var shrink = new List<int>(flex.Children.Count);
        for (var i = 0; i < flex.Children.Count; i++)
        {
            var source = flex.Children[i];
            var flexible = source as Flexible;
            var child = flexible?.Child ?? source;
            if (child is Spacer) continue;

            // A declared basis also BOUNDS the measure, so text inside wraps at the width the item
            // is going to get rather than at the whole line's.
            var basis = flexible is { Basis: > 0 } ? flexible.Basis : 0f;
            var constraint = basis > 0 ? MathF.Min(basis, mainAvail) : mainAvail;
            var forChild = constraints.ForChild(constraint, crossMax - padCross);
            // A zero weight without a basis starts from its CONTENT (#680), as the single-line pass
            // measures it: the main axis is decided by what goes in it, so a Fill has nothing to fill.
            if (flexible is { Flex: 0, Basis: 0 })
                forChild = horizontal
                    ? forChild with { Width = forChild.Width.DecidedByContent(true) }
                    : forChild with { Height = forChild.Height.DecidedByContent(true) };
            var node = Measure(child, forChild, ctx, ctx.ChildPath(path, i, source));

            measured.Add(node);
            sources.Add(source);
            hypothetical.Add(basis > 0 ? basis : horizontal ? node.Bounds.Width : node.Bounds.Height);
            grow.Add(flexible?.Flex ?? 0);
            shrink.Add(flexible?.Shrink ?? 0);
        }

        // The size each item OCCUPIES: its hypothetical one until its line resolves it. A Flexible's
        // item keeps it even when the line neither grows nor shrinks, so a zero weight at a basis of
        // 540 around a 400 box occupies 540, as in a browser, and not the box's 400.
        var resolved = new List<float>(hypothetical);

        // Break into lines, measuring against the hypothetical sizes.
        var lines = new List<(int Start, int Count, float Main, float Cross)>();
        var lineStart = 0;
        var lineMain = 0f;
        var lineCross = 0f;
        for (var i = 0; i < measured.Count; i++)
        {
            var childMain = hypothetical[i];
            var childCross = horizontal ? measured[i].Bounds.Height : measured[i].Bounds.Width;
            var withGap = lineMain > 0 ? lineMain + flex.Gap + childMain : childMain;
            if (lineMain > 0 && withGap > mainAvail)
            {
                lines.Add((lineStart, i - lineStart, lineMain, lineCross));
                lineStart = i;
                lineMain = childMain;
                lineCross = childCross;
            }
            else
            {
                lineMain = withGap;
                lineCross = MathF.Max(lineCross, childCross);
            }
        }
        if (measured.Count > lineStart)
            lines.Add((lineStart, measured.Count - lineStart, lineMain, lineCross));

        // Resolve each LINE on its own — the second pass CSS makes, and the piece that was missing.
        // Leftover goes to the growers by weight; an overflowing line is taken back from the
        // shrinkers weighted by basis (as CSS scales it), never past the min-content floor the
        // engine already computes. That floor is this pass's and not the web's: the web writes every
        // Flexible `min-width: 0`, so a browser takes a shrinking one past its child's min-content,
        // and a Flexible around a 400-wide box in a wrapping row of 300 is 300 there and 400 here.
        // A child whose main size actually moved is measured again, so its text re-wraps and its
        // cross size is the one it will really occupy.
        if (!float.IsPositiveInfinity(mainAvail))
        {
            for (var l = 0; l < lines.Count; l++)
            {
                var line = lines[l];
                var slack = mainAvail - line.Main;
                var totalGrow = 0;
                var scaledShrink = 0f;
                for (var i = line.Start; i < line.Start + line.Count; i++)
                {
                    totalGrow += grow[i];
                    scaledShrink += shrink[i] * hypothetical[i];
                }

                var growing = slack > 0.01f && totalGrow > 0;
                var shrinking = slack < -0.01f && scaledShrink > 0;
                if (!growing && !shrinking) continue;

                var resolvedMain = 0f;
                var resolvedCross = 0f;
                for (var i = line.Start; i < line.Start + line.Count; i++)
                {
                    var size = hypothetical[i];
                    if (growing && grow[i] > 0)
                        size += slack * grow[i] / totalGrow;
                    else if (shrinking && shrink[i] > 0)
                    {
                        size += slack * (shrink[i] * hypothetical[i]) / scaledShrink;
                        size = MathF.Max(size, horizontal ? MinContentWidth(sources[i], ctx) : 0);
                    }

                    if (MathF.Abs(size - hypothetical[i]) > 0.01f)
                    {
                        // Measured again inside the size it resolved to, so text re-wraps there. The
                        // item built below OCCUPIES that size, even when its content is shorter:
                        // otherwise the ones after it slide left and the line no longer fills what
                        // it was given.
                        var child = sources[i] is Flexible f ? f.Child : sources[i];
                        measured[i] = Measure(child, constraints.ForChild(horizontal ? size : crossMax - padCross,
                            horizontal ? crossMax - padCross : size) with { WidthIsACeiling = MainSizeIsACeiling(SizedBy(measured[i]).Source, horizontal) },
                            ctx, ctx.ChildPath(path, i, sources[i]));
                        resolved[i] = size;
                    }

                    resolvedMain += size + (i > line.Start ? flex.Gap : 0);
                    resolvedCross = MathF.Max(resolvedCross,
                        horizontal ? measured[i].Bounds.Height : measured[i].Bounds.Width);
                }

                lines[l] = (line.Start, line.Count, resolvedMain, resolvedCross);
            }
        }

        // Each Flexible becomes an ITEM at the size it occupies, around its child, the shape the
        // single-line slot builds: a child with a size of its own keeps it, any other is pinned to
        // the item. Before this the child WAS the item, so on a line that neither grew nor shrank
        // a zero weight at a basis of 540 occupied its 400 box's width (or a 600 one's), where a
        // browser gives the item 540 and lets the box sit or overflow inside it. A scroller wider
        // than its item is measured again under the ceiling the web's `max-width: 100%` puts on it.
        for (var i = 0; i < measured.Count; i++)
        {
            if (sources[i] is not Flexible flexible) continue;
            var child = measured[i];
            if (MainSizeIsACeiling(SizedBy(child).Source, horizontal)
                && (horizontal ? child.Bounds.Width : child.Bounds.Height) > resolved[i] + 0.01f)
                child = Measure(flexible.Child, constraints.ForChild(horizontal ? resolved[i] : crossMax - padCross,
                    horizontal ? crossMax - padCross : resolved[i]) with { WidthIsACeiling = true },
                    ctx, ctx.ChildPath(path, i, flexible));
            measured[i] = FlexItem(flexible, child, resolved[i], horizontal, ctx);
        }

        // Container extents.
        var contentMain = 0f;
        foreach (var line in lines) contentMain = MathF.Max(contentMain, line.Main);
        var main = mainSize.Kind switch
        {
            SizeKind.Fixed => mainSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(mainMax) => mainMax,
            _ => contentMain + padMain,
        };
        var contentCross = 0f;
        foreach (var line in lines) contentCross += line.Cross;
        if (lines.Count > 1) contentCross += runGap * (lines.Count - 1);
        var cross = crossSize.Kind switch
        {
            SizeKind.Fixed => crossSize.Value,
            SizeKind.Fill when !float.IsPositiveInfinity(crossMax) => crossMax,
            _ => contentCross + padCross,
        };

        // Arrange line by line.
        var crossCursor = horizontal ? flex.Padding.Top : flex.Padding.Start;
        foreach (var line in lines)
        {
            var free = MathF.Max(0, (main - padMain) - line.Main);
            var mainCursor = (horizontal ? flex.Padding.Start : flex.Padding.Top) + flex.Main switch
            {
                MainAlign.Center => free / 2,
                MainAlign.End => free,
                _ => 0,
            };
            var betweenExtra = flex.Main == MainAlign.SpaceBetween && line.Count > 1 ? free / (line.Count - 1) : 0;

            for (var i = line.Start; i < line.Start + line.Count; i++)
            {
                var child = measured[i];
                var childMain = horizontal ? child.Bounds.Width : child.Bounds.Height;
                var childCross = horizontal ? child.Bounds.Height : child.Bounds.Width;
                var alignment = sources[i].AlignSelf ?? flex.Cross;
                var within = alignment switch
                {
                    CrossAlign.Center => (line.Cross - childCross) / 2,
                    CrossAlign.End => line.Cross - childCross,
                    _ => 0,
                };
                if (alignment == CrossAlign.Stretch)
                {
                    child.Bounds = horizontal
                        ? child.Bounds with { Height = line.Cross }
                        : child.Bounds with { Width = line.Cross };
                    // A Flexible's item stretches THROUGH to its child, which is what this pass
                    // stretched before the child had an item around it.
                    if (sources[i] is Flexible && child.Children.Count > 0)
                    {
                        var inner = child.Children[0];
                        inner.Bounds = horizontal
                            ? inner.Bounds with { Height = line.Cross }
                            : inner.Bounds with { Width = line.Cross };
                    }
                    within = 0;
                }

                child.Bounds = horizontal
                    ? child.Bounds with { X = mainCursor, Y = crossCursor + within }
                    : child.Bounds with { X = crossCursor + within, Y = mainCursor };
                result.Adopt(child);
                mainCursor += childMain + flex.Gap + betweenExtra;
            }

            crossCursor += line.Cross + runGap;
        }

        result.Bounds = horizontal ? new Rect(0, 0, main, cross) : new Rect(0, 0, cross, main);
        return result;
    }
}
