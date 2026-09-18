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
        // additionally "lose to content" when space is tight (leftover floors at 0).
        var hasFlexibles = false;
        foreach (var c in flex)
            if (c is Flexible or Spacer { Flex: > 0 }) { hasFlexibles = true; break; }
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
            if (child is Text) return false;                       // text sizes itself
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
        // regardless of how the container itself is sized.
        LayoutNode MeasureChild(VisualNode child, float w, float h, string childPath,
            bool mainGranted = false, StretchKind stretchW = StretchKind.None,
            StretchKind stretchH = StretchKind.None, bool truncating = false)
        {
            var forChild = constraints.ForChild(w, h)
                .DecidedByContent(
                    childIndetW && !(mainGranted && horizontal),
                    childIndetH && !(mainGranted && !horizontal))
                .Stretched(stretchW, stretchH);
            return Measure(child, truncating ? forChild.Truncated() : forChild, ctx, childPath);
        }

        var children = flex.Children;
        var laid = new LayoutNode?[children.Count];
        var mains = new float[children.Count];
        var flexWeights = new float[children.Count];
        var gapTotal = flex.Gap * MathF.Max(0, children.Count - 1);

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
        // finite — a Hug row inside a bounded parent must not overflow it either.
        if (!float.IsPositiveInfinity(mainAvail) && rigidSum + gapTotal > mainAvail && horizontal)
        {
            var deficit = rigidSum + gapTotal - mainAvail;
            var textTotal = 0f;
            // `laid[i] is null` means pass 1 DEFERRED this child — a flexible, or a spacer with a
            // weight — and pass 2 sizes it from the leftover. It has no main extent to reduce, and
            // writing one here would be reaching into the other pass's half of the algorithm.
            // Inert as the arithmetic stands (a deferred child's main is 0, so its share of the
            // deficit is 0 too); the condition is the scope of this loop, not a repair.
            for (var i = 0; i < children.Count; i++)
                if (laid[i] is not null && TextWithin(children[i]) is not null) textTotal += mains[i];

            if (textTotal > 0)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    // A TEXT CHILD, seen through layout-transparent wrappers. Asking `is Text` here
                    // is what made `Pressable(Text(…))` run past the end of a fixed row: not a text,
                    // so not cut, so its floor was its longest word and nothing could shrink it.
                    if (laid[i] is null || TextWithin(children[i]) is null) continue;
                    var reduced = MathF.Max(0, mains[i] - deficit * (mains[i] / textTotal));
                    // The cut is a RE-MEASURE of the item, through the same pass everything else
                    // takes, carrying the line cap on the constraints. It used to be built here by
                    // hand from `ctx.Measurer` — which could only ever cut a bare Text, dropped a
                    // rich text's runs on the floor by rebuilding the node from PlainContent, and
                    // had already once measured against a different face than the one drawn.
                    var (tsW, tsH) = CrossStretch(children[i]);
                    var recut = MeasureChild(children[i], horizontal ? reduced : crossAvail,
                        horizontal ? crossAvail : reduced, ctx.ChildPath(path, i, children[i]),
                        stretchW: tsW, stretchH: tsH, truncating: true);
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
                var shrinkTotal = 0f;
                for (var i = 0; i < children.Count; i++)
                    if (Shrinkable(children[i])) shrinkTotal += mains[i];

                if (shrinkTotal > 0)
                {
                    // Two passes, because a floor one item refuses to cross is width the OTHERS
                    // have to give: pass 1 finds how much is really available to take, pass 2
                    // takes it. This is what makes a hugging button keep its word while the
                    // stretchy one beside it absorbs the overflow, exactly as a browser does.
                    var floors = new float[children.Count];
                    var yielding = 0f;
                    for (var i = 0; i < children.Count; i++)
                    {
                        if (!Shrinkable(children[i])) continue;
                        floors[i] = MathF.Min(mains[i], MinContentWidth(children[i], ctx));
                        yielding += mains[i] - floors[i];
                    }

                    var taking = MathF.Min(deficit, yielding);
                    if (taking > 0)
                    {
                        for (var i = 0; i < children.Count; i++)
                        {
                            if (!Shrinkable(children[i])) continue;
                            var room = mains[i] - floors[i];
                            if (room <= 0) continue;
                            var bound = MathF.Max(floors[i], mains[i] - taking * (room / yielding));
                            var childMaxW2 = horizontal ? bound : crossAvail;
                            var childMaxH2 = horizontal ? crossAvail : bound;
                            var (rsW, rsH) = CrossStretch(children[i]);
                            var reflowed = MeasureChild(children[i], childMaxW2, childMaxH2,
                                ctx.ChildPath(path, i, children[i]), stretchW: rsW, stretchH: rsH);
                            laid[i] = reflowed;
                            var shrunk = horizontal ? reflowed.Bounds.Width : reflowed.Bounds.Height;
                            rigidSum -= mains[i] - shrunk;
                            mains[i] = shrunk;
                        }
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
                var childMaxW = horizontal ? share : crossAvail;
                var childMaxH = horizontal ? crossAvail : share;
                // A Flexible is layout-transparent: whatever the container would stretch, it
                // stretches THROUGH it. Without this the wrapper grew to the cell and the content
                // inside it stayed at its own width, which is exactly what the tab labels did.
                var (fsW, fsH) = CrossStretch(flexible);
                // The share IS the slot's main size (the bounds are pinned to it below), so the
                // child is stretched on the main axis too: an auto-sized cell takes the share and
                // lays out inside it — a flex-grow item's autos fill the cell, per CSS.
                // The share IS the slot's main size, so the child is stretched on the main axis
                // too, on top of whatever the cross axis granted.
                var child = MeasureChild(flexible.Child, childMaxW, childMaxH,
                    ctx.ChildPath(ctx.ChildPath(path, i, flexible), 0), mainGranted: true,
                    stretchW: horizontal ? StretchKind.Flex : fsW,
                    stretchH: horizontal ? fsH : StretchKind.Flex);
                // The flexible slot IS the share on the main axis (the child fills it).
                child.Bounds = horizontal
                    ? child.Bounds with { Width = share }
                    : child.Bounds with { Height = share };
                var wrapper = ctx.Node(flexible, child.Bounds);
                wrapper.Adopt(child);
                laid[i] = wrapper;
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
            if (alignment == CrossAlign.Stretch && children[i] is not Text
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
    /// Spec S3 — the wrapping flex pass (CSS flex-wrap twin, v1 scope): children measure at their
    /// NATURAL size and break onto a new line when the next one would overflow the main extent.
    /// Each line arranges with the container's <see cref="FlexNode.Main"/>; within its line a child
    /// follows <see cref="FlexNode.Cross"/> (or its own AlignSelf); lines stack with RunGap.
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

        // Measure every child at its HYPOTHETICAL main size — its basis when it declares one, its
        // natural size otherwise. This is the number the line breaker works from, exactly as CSS
        // does: a pane with a basis of 440 asks for 440 whatever its content happens to measure, so
        // two of them share a line while there is room for both and take a line each when there is
        // not. A basis of 0 (the default) reproduces the old behaviour, where a Flexible simply
        // degraded to its child.
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
            var node = Measure(child, constraints.ForChild(constraint, crossMax - padCross), ctx, ctx.ChildPath(path, i, source));

            measured.Add(node);
            sources.Add(source);
            hypothetical.Add(basis > 0 ? basis : horizontal ? node.Bounds.Width : node.Bounds.Height);
            grow.Add(flexible?.Flex ?? 0);
            shrink.Add(flexible?.Shrink ?? 0);
        }

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
        // shrinkers weighted by basis (as CSS scales it) and never past the min-content floor the
        // engine already computes. A child whose main size actually moved is measured again, so its
        // text re-wraps and its cross size is the one it will really occupy.
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
                        var child = sources[i] is Flexible f ? f.Child : sources[i];
                        var remeasured = Measure(child, constraints.ForChild(horizontal ? size : crossMax - padCross,
                            horizontal ? crossMax - padCross : size), ctx, ctx.ChildPath(path, i, sources[i]));
                        // A flex item OCCUPIES the size it resolved to, even when its content is
                        // shorter — otherwise the ones after it slide left and the line no longer
                        // fills what it was given.
                        remeasured.Bounds = horizontal
                            ? remeasured.Bounds with { Width = size }
                            : remeasured.Bounds with { Height = size };
                        measured[i] = remeasured;
                    }

                    resolvedMain += size + (i > line.Start ? flex.Gap : 0);
                    resolvedCross = MathF.Max(resolvedCross,
                        horizontal ? measured[i].Bounds.Height : measured[i].Bounds.Width);
                }

                lines[l] = (line.Start, line.Count, resolvedMain, resolvedCross);
            }
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
                if (alignment == CrossAlign.Stretch && sources[i] is not Text)
                {
                    child.Bounds = horizontal
                        ? child.Bounds with { Height = line.Cross }
                        : child.Bounds with { Width = line.Cross };
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
