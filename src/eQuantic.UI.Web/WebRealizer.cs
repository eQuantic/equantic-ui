using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The WEB REALIZER for the shared abstract vocabulary (docs/SHARED-COMPONENTS-PLAN.md): lowers a
/// <see cref="VisualNode"/> tree to <see cref="HtmlElement"/>s — the same trees the native realizer
/// lowers to Photon pixels. Colors lower as <c>light-dark()</c> values straight from the tokens, so
/// the produced DOM is MODE-FREE like the abstract tree (theme switching = <c>color-scheme</c>).
/// This is the server-side (SSR) lowering; the TypeScript runtime mirrors these exact rules
/// client-side, and hydration correctness depends on the two staying identical — every mapping rule
/// here is therefore normative.
///
/// Layout parity notes (v1): flex maps 1:1 (direction/gap/justify/align, Flexible → <c>flex: n s basis</c>
/// matching the native leftover-by-weight semantics); Photon's inside border maps to
/// <c>box-sizing: border-box</c>; the cross-target layout harness tightens the remainder (plan).
/// </summary>
public static class WebRealizer
{
    public static HtmlElement Lower(VisualNode node, IAppTheme theme, float typeScale = 1f)
        => Lower(node, theme, typeScale, styles: null);

    /// <summary>
    /// Lower with ATOMIC style emission (docs/STYLE-SEMANTICS-PLAN.md §2): when a
    /// <paramref name="styles"/> sink is given, every element's style object is converted into
    /// deduplicated atomic classes collected into the sink — the markup carries class names and the
    /// sink carries the (once-per-declaration) rules. Without a sink, styles stay inline (tests and
    /// standalone lowering keep the direct form).
    /// </summary>
    public static HtmlElement Lower(VisualNode node, IAppTheme theme, float typeScale, StyleSink? styles)
    {
        var context = new ComponentContext(theme, typeScale);
        var root = LowerNode(node, context, horizontalAxis: null)
               ?? new RealizedElement("span"); // layout-only nodes outside a flex row lower to nothing
        if (styles != null)
            StyleAtomizer.AtomizeTree(root, ThemeVarMap.For(theme), styles);
        return root;
    }

    /// <summary>
    /// The states this subtree is being DRAWN as, if any. AsyncLocal like the style sink and for
    /// the same reason: SSR renders concurrent requests, and a static field would leak one page's
    /// preview into another's.
    /// </summary>
    private static readonly AsyncLocal<SimulatedState> _simulated = new();

    /// <summary>
    /// Draws the subtree in the given states. The web cannot force <c>:hover</c> or
    /// <c>:focus-visible</c> from CSS — nothing can, which is the point of the pseudo-class — so
    /// the diffs those rules carry are folded into the BASE style instead. What the reader sees is
    /// what a real hover would produce, because it is the same declarations.
    /// </summary>
    private static HtmlElement? LowerSimulated(Simulated simulated, ComponentContext context,
        bool? horizontalAxis)
    {
        var previous = _simulated.Value;
        // Nested previews COMBINE rather than replace: a hovered card containing a pressed button
        // is two nodes, and the inner one must not turn the outer's hover off.
        _simulated.Value = previous | simulated.State;
        try
        {
            return LowerNode(simulated.Child, context, horizontalAxis);
        }
        finally
        {
            _simulated.Value = previous;
        }
    }

    /// <summary>
    /// Arms the in-flow intent while the child BUILDS. The overlay reads it in its own Build — that
    /// is the only moment the answer can change anything, because by the time a tree exists the
    /// scrim and the layer are already in it.
    /// </summary>
    private static HtmlElement? LowerInFlow(InFlow inFlow, ComponentContext context, bool? horizontalAxis)
    {
        var previous = Primitives.InFlow.Current;
        Primitives.InFlow.Current = true;
        try
        {
            return LowerNode(inFlow.Child, context, horizontalAxis);
        }
        finally
        {
            Primitives.InFlow.Current = previous;
        }
    }

    /// <summary>
    /// A layout-transparent wrapper the runtime observes. The declaration rides a stable path the
    /// client picks up after the pass mounts — the same shape ScrollView's viewport reporting uses,
    /// because the answer is not knowable until layout has happened.
    /// </summary>
    private static HtmlElement? LowerInView(InView inView, ComponentContext context, bool? horizontalAxis)
    {
        var child = LowerNode(inView.Child, context, horizontalAxis);
        if (child is null) return null;

        var wrapper = new RealizedElement("div")
        {
            // display:contents — the wrapper reports, it does not lay out. A div in the flow would
            // break the grid or flex the child was written to sit in.
            //
            // The OBSERVER marker is stamped by the client, which is the only side that can watch
            // anything; the server emits the same element so the two trees have the same shape and
            // hydration is an attribute diff rather than a replaced subtree (the same split
            // ScrollView's viewport reporting uses).
            Style = new HtmlStyle { Display = Core.Display.Contents },
        };
        wrapper.Children.Add(child);
        return wrapper;
    }

    /// <summary>
    /// The single dispatch every node passes through — and therefore the one place the design-mode
    /// origin is attached, once, rather than in each of the branches below.
    /// <para>
    /// <see cref="VisualNode.Origin"/> is set only by a design-mode compilation, so in every shipped
    /// build this is one null check per node and the DOM is byte-identical. It has to stay the exact
    /// twin of the same step in <c>lowering.ts</c>: the two producers render the same tree on either
    /// side of hydration, and one attribute of disagreement is a diverged subtree.
    /// </para>
    /// </summary>
    private static HtmlElement? LowerNode(VisualNode node, ComponentContext context, bool? horizontalAxis)
    {
        var lowered = LowerNodeKind(node, context, horizontalAxis);
        if (lowered is not null && node.Origin is { Length: > 0 } origin)
        {
            // DataAttributes and not RawAttributes: RawAttributes lives on RealizedElement (it exists
            // so SVG can emit viewBox/d verbatim), and not every lowered node is one. DataAttributes
            // is on HtmlElement itself and prefixes `data-`, so this reaches every node there is.
            var data = lowered.DataAttributes ??= new Dictionary<string, string>();
            data["eq-origin"] = origin;
            if (node.OriginLabel is { Length: > 0 } label) data["eq-component"] = label;
        }
        return lowered;
    }

    private static HtmlElement? LowerNodeKind(VisualNode node, ComponentContext context, bool? horizontalAxis) => node switch
    {
        Box box => LowerBox(box, context),
        FlexNode flex => LowerFlex(flex, context),
        Stack stack => LowerStack(stack, context),
        Grid grid => LowerGrid(grid, context),
        AdaptiveNode adaptive => LowerAdaptive(adaptive, context),
        Sticky sticky => LowerSticky(sticky, context),
        SafeArea safeArea => LowerSafeArea(safeArea, context),
        Anchored anchored => LowerAnchored(anchored, context),
        ScrollView scroll => LowerScrollView(scroll, context),
        // A Positioned outside a Stack has no anchor frame — degrade to its child (parity with native).
        Positioned positioned => LowerNode(positioned.Child, context, horizontalAxis),
        Text text => LowerText(text, context),
        TextEntry entry => LowerTextEntry(entry, context),
        Overlay overlay => LowerOverlay(overlay, context),
        Icon icon => LowerIcon(icon, context),
        Vector vector => LowerVector(vector),
        Drawing drawing => LowerDrawing(drawing),
        Spinner spinner => LowerSpinner(spinner, context),
        Primitives.Image image => LowerImage(image),
        CameraPreview camera => LowerCameraPreview(camera),
        WebFrame frame => LowerWebFrame(frame),
        Pressable pressable => LowerPressable(pressable, context),
        Hoverable hoverable => LowerHoverable(hoverable, context),
        Simulated simulated => LowerSimulated(simulated, context, horizontalAxis),
        InFlow inFlow => LowerInFlow(inFlow, context, horizontalAxis),
        InView inView => LowerInView(inView, context, horizontalAxis),
        Adjustable adjustable => LowerAdjustable(adjustable, context),
        Navigable navigable => LowerNavigable(navigable, context),
        Shortcut shortcut => LowerShortcut(shortcut, context, horizontalAxis),
        Link link => LowerLink(link, context),
        LoopMotion motion => LowerLoopMotion(motion, context),
        Presence presence => LowerPresence(presence, context, horizontalAxis),
        DragDismiss drag => LowerDragDismiss(drag, context, horizontalAxis),
        Draggable draggable => LowerDraggable(draggable, context, horizontalAxis),
        Flexible flexible => LowerFlexible(flexible, context, horizontalAxis),
        Spacer spacer => LowerSpacer(spacer, horizontalAxis),
        // BuildContained, never Build: one component's throw must cost that component's subtree and
        // nothing else — on the server it used to cost the whole request (a 500 for a card).
        UiComponent component => LowerNode(component.BuildContained(context), context, horizontalAxis),
        _ => null,
    };

    /// <summary>
    /// Spec A3 lowering: single-cell CSS grid — every NON-positioned child sits in cell 1/1
    /// (overlapping, painted in child order) with <c>place-items</c> carrying the alignment;
    /// Positioned children wrap in <c>position:absolute</c> against the stack's
    /// <c>position:relative</c> frame, with signed offsets (End→right, Start→left).
    /// </summary>
    /// <summary>
    /// A Stack child as the STACK sees it: a component is expanded to what it builds, so a
    /// `Positioned` returned by one still positions. Only for the positioning decision — the node
    /// that comes back is lowered normally.
    /// </summary>
    private static VisualNode ResolveForPositioning(VisualNode child, ComponentContext context)
    {
        // Bounded: a component whose build returns itself would otherwise spin here.
        for (var hops = 0; hops < 8 && child is UiComponent component; hops++)
        {
            child = component.BuildContained(context);
        }
        return child;
    }

    private static HtmlElement LowerStack(Stack stack, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Display = Core.Display.Grid,
                Position = Core.Position.Relative,
                Width = Size(stack.Width),
                Height = Size(stack.Height, vertical: true),
                FlexShrink = Rigid(stack.Width, stack.Height),
            },
        };

        // Spec A3: paint order IS child order. On web that needs an EXPLICIT z-index per cell —
        // a child carrying backdrop-filter/filter/opacity creates a stacking context, which CSS
        // paints as if positioned at z-index 0, i.e. ABOVE every plain sibling that follows it in
        // the DOM. (A blurred scrim would otherwise cover the dialog it sits behind.)
        var depth = 0;
        foreach (var raw in stack.Children)
        {
            depth++;
            // Through the COMPONENT to what it builds. `Positioned` is a contract with the parent
            // — like a flex weight, it means nothing anywhere else — and asking `child is
            // Positioned` missed the moment one came out of a component. It then degraded to its
            // child and joined the flow: a corner button rendered ABOVE the slab it belonged to,
            // silently, which is worse than not rendering at all.
            var child = ResolveForPositioning(raw, context);
            if (child is Positioned positioned)
            {
                var lowered = LowerNode(positioned.Child, context, horizontalAxis: null);
                if (lowered is null) continue;
                // An absolutely-positioned box with ONE edge shrink-wraps its content; with two it
                // spans between them. Native measures a positioned child against the stack's full
                // extent, so a child that FILLS gets the stack's width there and 64px of button
                // here — the same tree, two geometries. Pinning the opposite edge gives the box the
                // definite width its filling child is asking to be 100% of.
                var (fillsWidth, fillsHeight) = Fills(positioned.Child);
                var spanX = fillsWidth && positioned.Start is null != positioned.End is null;
                var spanY = fillsHeight && positioned.Top is null != positioned.Bottom is null;
                var anchor = new RealizedElement("div")
                {
                    Style = new HtmlStyle
                    {
                        Position = Core.Position.Absolute,
                        Top = positioned.Top is { } top ? TokenCss.Px(top) : spanY ? "0" : null,
                        Right = positioned.End is { } end ? TokenCss.Px(end) : spanX ? "0" : null,
                        Bottom = positioned.Bottom is { } bottom ? TokenCss.Px(bottom) : spanY ? "0" : null,
                        Left = positioned.Start is { } start ? TokenCss.Px(start) : spanX ? "0" : null,
                        // Spec S7: explicit stacking WINS; otherwise the child's own depth.
                        ZIndex = (positioned.ZIndex != 0 ? positioned.ZIndex : depth).ToString(),
                    },
                };
                anchor.Children.Add(lowered);
                element.Children.Add(anchor);
            }
            else
            {
                var lowered = LowerNode(child, context, horizontalAxis: null);
                if (lowered is null) continue;
                // The cell IS the stack's available space (the native MeasureStack contract): it
                // stretches to the single grid cell and aligns its child via flex — so a Fill child
                // covers the stack while a hug child sits at the Stack.Align anchor.
                var cell = new RealizedElement("div")
                {
                    Style = new HtmlStyle
                    {
                        GridArea = "1 / 1",
                        Display = Core.Display.Flex,
                        JustifyContent = AlignmentJustify(stack.Align),
                        AlignItems = AlignmentAlign(stack.Align),
                        Width = "100%",
                        Height = "100%",
                        // …and the cell may not grow PAST it. A grid item's automatic minimum size
                        // is its min-content size, so one layer holding a scroller sizes the track
                        // to the scroller's content and the whole stack swells to the widest line in
                        // the file — the scrollbar disappears and the overflow is clipped by
                        // whatever ancestor happens to be smaller. Native measures a layer against
                        // the stack's own extent, which is exactly what min-0 restores.
                        MinWidth = "0",
                        MinHeight = "0",
                        // A grid item takes z-index without needing `position` — this is what keeps
                        // a filtered child from jumping above the siblings drawn after it.
                        ZIndex = depth.ToString(),
                        // …and a cell whose LAYER is intangible has to be intangible too. The cell
                        // stretches to the whole stack and carries the layer's z-index, so a closed
                        // Drawer (a bare Box, see PaintsNothing) covered the viewport with an
                        // invisible interactive rectangle: on a phone the shell's own menu button
                        // could not be tapped. Marking only the inner box left the cell in the way.
                        PointerEvents = child is Box bare && PaintsNothing(bare) ? "none" : null,
                    },
                };
                cell.Children.Add(lowered);
                element.Children.Add(cell);
            }
        }

        return element;
    }

    /// <summary>Spec A6 lowering: native browser scrolling — <c>overflow-y/x: auto</c> on the axis
    /// (the browser owns physics, momentum and the scrollbar); the cross axis stays hidden so content
    /// never leaks. The programmatic Offset is a native-side concept (browser scroll state lives in
    /// the DOM).</summary>
    /// <summary>Spec S7: scroll-anchored chrome — in flow until scrolling would push it out, then
    /// pinned at <c>Offset</c> from the viewport start (CSS sticky; v1 vertical).</summary>
    /// <summary>
    /// Wave 3 anchored overlay: a position:relative host wrapping the anchor; while Open, an
    /// invisible fixed scrim (a real Pressable — tap-outside dismisses through the ordinary event
    /// pipeline) and the absolute panel, positioned ENTIRELY by the generated placement classes
    /// (no JS). The gap rides the margin on the placement axis as an atomic declaration. Keep
    /// anchors out of overflow-hidden scrollers and transformed subtrees (the documented fences).
    /// </summary>
    private static HtmlElement LowerAnchored(Anchored anchored, ComponentContext context)
    {
        var host = new RealizedElement("div")
        {
            // Hover reveal (wave 3b, the Tooltip mechanism): the panel stays in the DOM and the
            // generated .eq-hoverreveal rules show it on :hover — pure CSS, no scrim, no state.
            ClassName = anchored.OpenOnHover ? "eq-anchorhost eq-hoverreveal" : "eq-anchorhost",
        };
        // The tooltip semantics need the PANEL's id before the anchor renders, and the id needs
        // the panel's text — lowered once, reused for both.
        var describedBy = (string?)null;
        if (anchored is { DescribesAnchor: true, OpenOnHover: true })
            describedBy = $"eq-tip-{StyleAtomizer.Hash(TextContentOf(anchored.Panel))}";

        // The menu/listbox panel id: FNV of the panel's text. The TS producer hashes its PATH
        // instead (unique when an adaptive shell mounts the same menu twice) — the spellings may
        // differ because an OPEN panel never comes from SSR, so hydration never compares them.
        // An app that does SSR an open listbox gets a benign id patch on hydration, nothing more.
        var panelId = anchored.PanelRole != AnchorPanelRole.None
            ? $"eq-panel-{StyleAtomizer.Hash(TextContentOf(anchored.Panel))}"
            : null;

        if (LowerNode(anchored.Anchor, context, horizontalAxis: null) is { } anchor)
        {
            // What a screen reader reads AFTER the control's name — whether or not the panel is
            // visually revealed. On the anchor's root, which for the ordinary Tooltip(child:
            // IconButton) IS the focusable button.
            if (describedBy is not null) anchor.AriaDescribedBy = describedBy;

            // The anchor's half of the menu/listbox pattern, on its root — the trigger button.
            if (panelId is not null)
            {
                anchor.AriaHasPopup = anchored.PanelRole switch
                {
                    AnchorPanelRole.Listbox => "listbox",
                    AnchorPanelRole.Dialog => "dialog",
                    _ => "menu",
                };
                // A Select's FIELD is the combobox — the role rides the trigger, not the panel.
                if (anchored.PanelRole == AnchorPanelRole.Listbox) anchor.Role = "combobox";
                if (anchored.Open)
                {
                    anchor.AriaControls = panelId;
                    // The highlight the arrows move, stated — otherwise it is only paint, and a
                    // screen reader following the keyboard hears nothing move.
                    if (anchored.ActiveIndex >= 0)
                        anchor.AriaActiveDescendant = $"{panelId}-{anchored.ActiveIndex}";
                }
            }
            // A painted scrim dims the PAGE, never its own anchor — the anchor lifts above it.
            // Snap panels gate the class on Open (position+z the closed state shouldn't pay for);
            // Motion panels keep it ALWAYS — the scrim is still fading out after close, and an
            // anchor that dropped behind it would dim mid-exit.
            if (anchored is { ScrimStyle: not null, OnDismiss: not null }
                && (anchored.Open || anchored.Motion is not null))
                anchor.ClassName = string.IsNullOrEmpty(anchor.ClassName)
                    ? "eq-anchor-above" : $"{anchor.ClassName} eq-anchor-above";
            host.Children.Add(anchor);
        }

        if (anchored.OpenOnHover)
        {
            var hoverPanel = BuildAnchorPanel(anchored, context);
            if (describedBy is not null)
            {
                hoverPanel.Role = "tooltip";
                hoverPanel.Id = describedBy;
            }
            host.Children.Add(hoverPanel);
            return host;
        }

        // Open/close MOTION keeps the panel and scrim MOUNTED in both states (same tree shape →
        // element identity survives the swap → the CSS transitions glide); without it, closed
        // renders nothing (the historical snap).
        if (!anchored.Open && anchored.Motion is null) return host;

        // Mega-menu dimming: ScrimStyle PAINTS the outside-tap scrim (background/gradient/
        // backdrop-blur, a full Box) instead of the historical invisible one — one element serves
        // both the dismissal and the page veil.
        var scrimBox = new Box((anchored.ScrimStyle ?? default) with { Width = SizeValue.Fill, Height = SizeValue.Fill });
        if (anchored.OnDismiss is { } dismiss
            && LowerNode(new Pressable(scrimBox, dismiss) { Label = "Dismiss" }, context, horizontalAxis: null) is { } scrim)
        {
            scrim.ClassName = string.IsNullOrEmpty(scrim.ClassName)
                ? "eq-anchor-scrim" : $"{scrim.ClassName} eq-anchor-scrim";
            if (anchored.Motion is { } scrimMotion)
            {
                // The scrim cross-fades with the panel; closed it is hidden — invisible, out of
                // hit-testing AND the focus order (visibility rides the same transition, flipping
                // at the fade's end on exit / start on enter, the CSS-native contract).
                scrim.Style ??= new HtmlStyle();
                scrim.Style.Transition = MotionTransition(scrimMotion, withTransform: false);
                if (!anchored.Open)
                {
                    scrim.Style.Opacity = "0";
                    scrim.Style.Visibility = "hidden";
                    scrim.Style.PointerEvents = "none";
                }
            }
            host.Children.Add(scrim);
        }

        var openPanel = BuildAnchorPanel(anchored, context);
        if (panelId is not null)
        {
            openPanel.Role = anchored.PanelRole switch
            {
                AnchorPanelRole.Listbox => "listbox",
                // A panel holding a COMPOSITE (C15's calendar): focus moves INTO it, so it is a
                // dialog, and its rows are not items the trigger's activedescendant can point at.
                AnchorPanelRole.Dialog => "dialog",
                _ => "menu",
            };
            openPanel.Id = panelId;
            // Number the item rows IN TREE ORDER — the ids aria-activedescendant points at. Tree
            // order is the one thing both producers agree on without sharing any state.
            var next = 0;
            NumberItemRows(openPanel, panelId, ref next);
        }
        host.Children.Add(openPanel);
        return host;
    }

    /// <summary>Walks the lowered panel and ids every menuitem/option row: <c>{panelId}-0</c>,
    /// <c>-1</c>… in tree order — the TS twin numbers the same way.</summary>
    private static void NumberItemRows(Core.HtmlElement element, string panelId, ref int next)
    {
        if (element.Role is "menuitem" or "option")
        {
            element.Id = $"{panelId}-{next}";
            next++;
        }
        foreach (var child in element.Children)
            if (child is Core.HtmlElement childElement)
                NumberItemRows(childElement, panelId, ref next);
    }

    /// <summary>The panel's visible text, flattened — what the tooltip id hashes. Walking the
    /// VISUAL tree (not the lowered one) keeps the answer identical on both producers.
    /// <para>
    /// PlainContent, not Content: a paragraph built from RUNS has an empty Content, so every
    /// styled panel hashed the same empty string and shared one id with all the others — and the
    /// aria-describedby pointing at it then named somebody else's panel.
    /// </para></summary>
    private static string TextContentOf(VisualNode node) => node switch
    {
        Text text => text.PlainContent,
        Box box => box.Child is { } child ? TextContentOf(child) : "",
        FlexNode flex => string.Concat(flex.Children.Select(TextContentOf)),
        Pressable pressable => TextContentOf(pressable.Child),
        _ => "",
    };

    private static RealizedElement BuildAnchorPanel(Anchored anchored, ComponentContext context)
    {
        var top = anchored.Placement is AnchorPlacement.TopStart or AnchorPlacement.TopEnd
            or AnchorPlacement.TopCenter;
        // HOVER BRIDGE: for hover-open panels the gap must be PADDING (part of the hoverable
        // area), not margin — crossing a margin gap leaves the host's :hover for a few pixels and
        // the panel vanishes before the pointer reaches it. Click-open panels keep the margin
        // (padding would catch the outside-tap on the gap strip).
        var bridge = anchored.OpenOnHover;
        var panel = new RealizedElement("div")
        {
            ClassName = "eq-anchor-panel " + PlacementClass(anchored.Placement)
                + (anchored.MatchAnchorWidth ? " eq-anchor-match" : ""),
            Style = new HtmlStyle
            {
                MarginTop = !top && !bridge ? TokenCss.Px(anchored.Gap) : null,
                MarginBottom = top && !bridge ? TokenCss.Px(anchored.Gap) : null,
                PaddingTop = !top && bridge ? TokenCss.Px(anchored.Gap) : null,
                PaddingBottom = top && bridge ? TokenCss.Px(anchored.Gap) : null,
            },
        };
        if (anchored.Motion is { } motion && !anchored.OpenOnHover)
        {
            // Open/close glide: fade + a MotionLiftDp nudge toward the anchor (up for bottom
            // placements, down for top). Closed ends visibility:hidden — gone from hit-testing
            // and the Tab order once the exit completes.
            panel.Style.Transition = MotionTransition(motion, withTransform: true);
            if (!anchored.Open)
            {
                panel.Style.Opacity = "0";
                panel.Style.Transform = $"translateY({TokenCss.Px(top ? Anchored.MotionLiftDp : -Anchored.MotionLiftDp)})";
                panel.Style.Visibility = "hidden";
                panel.Style.PointerEvents = "none";
            }
        }
        if (LowerNode(anchored.Panel, context, horizontalAxis: null) is { } content)
            panel.Children.Add(content);
        return panel;
    }

    /// <summary>
    /// The Anchored open/close transition list: opacity (+ transform for the panel) riding the
    /// spec's duration/easing, and ALWAYS visibility — it flips at the fade's end on exit and its
    /// start on enter (the CSS discrete-interpolation rule), which is what parks a closed panel
    /// outside hit-testing and the focus order without JS.
    /// </summary>
    private static string MotionTransition(TransitionSpec motion, bool withTransform)
    {
        var timing = $"{TokenCss.Number(motion.DurationMs)}ms {TokenCss.Bezier(motion.Easing)}";
        return withTransform
            ? $"opacity {timing}, transform {timing}, visibility {timing}"
            : $"opacity {timing}, visibility {timing}";
    }

    private static string PlacementClass(AnchorPlacement placement) => placement switch
    {
        AnchorPlacement.BottomEnd => "eq-anchor-b-end",
        AnchorPlacement.TopStart => "eq-anchor-t-start",
        AnchorPlacement.TopEnd => "eq-anchor-t-end",
        AnchorPlacement.BottomCenter => "eq-anchor-b-center",
        AnchorPlacement.TopCenter => "eq-anchor-t-center",
        _ => "eq-anchor-b-start",
    };

    /// <summary>
    /// The system's own margins, which only the HOST knows — so the browser fills them:
    /// <c>env(safe-area-inset-*)</c> is zero on a display with no cutouts and the real number on one
    /// that has them. An edge the caller left out gets its own padding, or none.
    /// </summary>
    private static HtmlElement LowerSafeArea(SafeArea safeArea, ComponentContext context)
    {
        string Inset(SafeEdges edge, string name, float extra)
        {
            var env = $"env(safe-area-inset-{name}, 0px)";
            if (!safeArea.Edges.HasFlag(edge)) return TokenCss.Px(extra);
            return extra == 0 ? env : $"calc({env} + {TokenCss.Px(extra)})";
        }

        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                PaddingTop = Inset(SafeEdges.Top, "top", safeArea.Extra.Top),
                PaddingBottom = Inset(SafeEdges.Bottom, "bottom", safeArea.Extra.Bottom),
                PaddingLeft = Inset(SafeEdges.Start, "left", safeArea.Extra.Start),
                PaddingRight = Inset(SafeEdges.End, "right", safeArea.Extra.End),
            },
        };

        if (LowerNode(safeArea.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// The stacking PLANES, because CSS gives you one number and a page needs three.
    /// <para>
    /// CONTENT is elevation 1–5 — the design system's own scale, and the only one an author names.
    /// CHROME is above all of it: a pinned header is not a raised card, and nothing an author does
    /// inside the content should be able to scroll over it. Overlays sit above chrome, positioned
    /// by their own layer rather than by a number.
    /// </para>
    /// <para>
    /// Chrome is TWO bands, not one. A floating header and a pinned rail are both chrome, and on
    /// one number they TIE — so the winner is whichever comes later in the document, which is the
    /// page's structure deciding a paint order nobody chose. Reported by the site: a rail pinned on
    /// one page painted over the open mega menu of the header, and the header's own panel could not
    /// climb out because its z-index lives inside the header's stacking context. Floating chrome
    /// outranks pinned chrome by construction, because a header whose dropdown falls behind the
    /// page is not a trade-off anybody wants to be offered.
    /// </para>
    /// <para>
    /// Photon needs none of this: paint order IS child order there, so a later sibling is on top and
    /// the question never comes up. This is the web realizer keeping that promise.
    /// </para>
    /// </summary>
    private const string PinnedLayer = "100";

    /// <summary>Floating chrome — see <see cref="PinnedLayer"/> for why it is a band of its own.</summary>
    private const string FloatingChromeLayer = "110";

    private static HtmlElement LowerSticky(Sticky sticky, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // FLOATING chrome (the fixed header) paints above the page and takes no layout
                // space; plain sticky stays in flow and pins when scrolling reaches it.
                Position = sticky.Float ? Core.Position.Fixed : Core.Position.Sticky,
                Top = TokenCss.Px(sticky.Offset),
                Left = sticky.Float ? "0" : null,
                Right = sticky.Float ? "0" : null,
                // CHROME, above anything the CONTENT can reach. Plain sticky used to sit at 1, one
                // step above nothing, which is a number competing with other numbers: a raised card
                // (elevation carries 1–5 now) or a deep enough stack cell would out-stack the pinned
                // header and scroll straight over it. Chrome is not "slightly raised content", it is
                // a different plane, and saying so here is what keeps the author out of the argument.
                // FLOATING chrome is a band above PINNED chrome, so a header and a rail on one page
                // do not tie and let document order pick the winner.
                ZIndex = sticky.Float ? FloatingChromeLayer : PinnedLayer,
                // Spec S6: the scrolled swap GLIDES (the design's transparent-until-scrolled bar
                // fades its veil in) instead of flipping in one frame.
                Transition = sticky.Transition is { } transition ? TokenCss.Transition(transition) : null,
            },
        };

        // SCROLL-LINKED diff: each declaration lands under the root-gated scrolled variant; the
        // runtime's scroll listener toggles `eq-scrolled` on <html>.
        if (sticky.ScrolledStyle is { IsEmpty: false } scrolled)
        {
            if (scrolled.Background is { } bg)
                element.ScrolledDeclarations.Add(("background-color", TokenCss.Value(bg)));
            if (scrolled is { BorderWidth: { } bw, BorderColor: { } bc })
                element.ScrolledDeclarations.Add(("border-bottom", $"{TokenCss.Px(bw)} solid {TokenCss.Value(bc)}"));
            if (scrolled.Opacity is { } alpha)
                element.ScrolledDeclarations.Add(("opacity", TokenCss.Number(alpha)));
            if (scrolled.BackdropBlur is { } blur and > 0)
            {
                element.ScrolledDeclarations.Add(("backdrop-filter", $"blur({TokenCss.Px(blur)})"));
                element.ScrolledDeclarations.Add(("-webkit-backdrop-filter", $"blur({TokenCss.Px(blur)})"));
            }
        }

        if (LowerNode(sticky.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    private static HtmlElement LowerScrollView(ScrollView scroll, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // A scroller takes the pointer — the wheel, the drag, the touch — so it declares
                // itself a target: `none` inherits from any transparent row above it, and a
                // scroller that is not a target does not scroll.
                PointerEvents = "auto",
                Width = Size(scroll.Width),
                // A scroll view IS the window its parent gives it — never its content. Native gets
                // that from layout (bounds in, clip always); an auto-height overflow div grows with
                // the content instead and scrolls nothing, so the web mirror defaults to 100%.
                Height = Size(scroll.Height, vertical: true) ?? "100%",
                OverflowY = scroll.Axis is ScrollAxis.Vertical or ScrollAxis.Both ? "auto" : "hidden",
                OverflowX = scroll.Axis is ScrollAxis.Horizontal or ScrollAxis.Both ? "auto" : "hidden",
                // …and it must never be PUSHED wider than that window either. A flex item's
                // min-width is `auto` by default, so one long line of code (the playground's
                // generated module) grew the scroller, its ancestors, and the whole 1440px page
                // to 3134px instead of scrolling. min-width:0 + max-width:100% break the chain
                // exactly where the promise lives; native gets it from layout for free.
                MinWidth = "0",
                MinHeight = "0",
                MaxWidth = "100%",
            },
        };
        var child = LowerNode(scroll.Child, context, horizontalAxis: null);
        if (child != null) element.Children.Add(child);
        return element;
    }

    /// <summary>CSS <c>place-items</c> = "&lt;align&gt; &lt;justify&gt;" (vertical then horizontal).</summary>
    /// <summary>Horizontal anchor of the 9-point alignment → flex justify-content.</summary>
    private static JustifyContent AlignmentJustify(Alignment align) => ((int)align % 3) switch
    {
        1 => JustifyContent.Center,
        2 => JustifyContent.FlexEnd,
        _ => JustifyContent.FlexStart,
    };

    /// <summary>Vertical anchor of the 9-point alignment → flex align-items.</summary>
    private static AlignItem AlignmentAlign(Alignment align) => ((int)align / 3) switch
    {
        1 => AlignItem.Center,
        2 => AlignItem.FlexEnd,
        _ => AlignItem.FlexStart,
    };

    /// <summary>
    /// Spec A10 lowering: inline 24×24-viewBox SVG with the registry's single alpha-mask path and
    /// <c>fill="currentColor"</c> — the tint rides the CSS <c>color</c> property exactly like text
    /// (token → light-dark()). Null color inherits; null label = decorative (aria-hidden).
    /// </summary>
    /// <summary>
    /// Spec B15, drawn inside the fence: an SVG of 8 rrect bars (2×5 in the 16 viewBox) rotated
    /// i·45° about the center; the phase stagger rides per-bar NEGATIVE animation-delays over the
    /// generated 800ms 1→0.3 fade (exact parity with the native f(t) alphas). Color inherits via
    /// currentColor exactly like Icon; the 400ms anti-flash appear delay and the Reduce Motion
    /// pulse-in-place (delays zeroed) live in the generated stylesheet.
    /// </summary>
    private static HtmlElement LowerSpinner(Spinner spinner, ComponentContext context)
    {
        var svg = new RealizedElement("svg")
        {
            ClassName = "eq-spinner",
            Style = new HtmlStyle
            {
                Width = TokenCss.Px(spinner.Size),
                Height = TokenCss.Px(spinner.Size),
                Color = spinner.Color is { } tint ? TokenCss.Value(tint) : null,
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["viewBox"] = "0 0 16 16",
                ["fill"] = "currentColor",
                ["aria-hidden"] = "true",
            },
        };

        var step = Spinner.RevolutionMs / 8;
        for (var i = 0; i < 8; i++)
        {
            var bar = new RealizedElement("rect")
            {
                Style = new HtmlStyle { AnimationDelay = $"-{i * step}ms" },
                RawAttributes = new Dictionary<string, string>
                {
                    ["x"] = "7",
                    ["y"] = "0",
                    ["width"] = "2",
                    ["height"] = "5",
                    ["rx"] = "1",
                    ["transform"] = $"rotate({i * 45} 8 8)",
                },
            };
            svg.Children.Add(bar);
        }
        return svg;
    }

    /// <summary>A vector shape lowers exactly like an icon — the differences are that its size is
    /// the author's rather than the §07 whitelist's, and that its box need not be square.</summary>
    private static HtmlElement LowerVector(Vector vector) =>
        LowerGlyph(vector.Glyph, vector.Size, vector.Height, vector.Color, vector.Label);

    private static HtmlElement LowerIcon(Icon icon, ComponentContext context)
        => LowerGlyph(icon.Glyph, icon.Size, icon.Size, icon.Color, icon.Label);

    /// <summary>
    /// Artwork stays VECTOR in the DOM: one inline <c>&lt;svg&gt;</c> carrying the drawing's own
    /// viewBox, and one <c>&lt;path&gt;</c> per shape with the paint the file chose. It scales
    /// without blurring, it prints, and it costs no request.
    /// <para>
    /// The shapes the file left as <c>currentColor</c> are emitted as <c>currentColor</c> — the
    /// word, not a resolved value — so the CSS colour on the wrapper answers them. That is what
    /// makes a monochrome mark follow the theme, and it is why the tint is set as `color` rather
    /// than painted into every path.
    /// </para>
    /// </summary>
    private static HtmlElement LowerDrawing(Drawing drawing)
    {
        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                // Block for the same reason a glyph is: an inline svg sits on the text baseline of
                // whatever holds it and picks up a line box it never asked for.
                Display = Core.Display.Block,
                Width = TokenCss.Px(drawing.Width),
                Height = TokenCss.Px(drawing.Height),
                Color = drawing.Tint is { } tint ? TokenCss.Value(tint) : null,
            },
        };
        svg.RawAttributes = new Dictionary<string, string>
        {
            ["viewBox"] = drawing.Artwork.ViewBox,
            // Each axis scales independently, matching the box the author asked for — the same
            // contract Vector states. A caller who wants the artwork undistorted omits the height
            // and gets the drawing's own aspect.
            ["preserveAspectRatio"] = "none",
            ["fill"] = "none",
        };
        if (drawing.Label is { } label) svg.RawAttributes["aria-label"] = label;
        else svg.RawAttributes["aria-hidden"] = "true";

        // Every run the artwork paints with, declared once and referenced by the shapes. The id is
        // the hash of the gradient ITSELF, so two shapes sharing a run share a def.
        //
        // WHERE that def goes is the document's business, not this drawing's. Inline, a second
        // drawing with the same run declares the same id again, and `url(#id)` binds to the first in
        // DOCUMENT order — which, once an AdaptiveNode puts every arm in the page, is the arm the
        // media query hides. A paint server in a display:none subtree is not rendered, so the shape
        // came out unpainted on the layout it was meant for. With a sink armed the runs go to the
        // page's one container instead; without one — a drawing realized alone, a unit test — they
        // stay inline, because there is no document to put a container in.
        if (GradientSink.Ambient is { } sink)
        {
            foreach (var gradient in Gradients(drawing.Artwork))
                sink.Add(gradient.Key, gradient.Value);
        }
        else
        {
            var defs = new RealizedElement("defs");
            foreach (var gradient in Gradients(drawing.Artwork))
                defs.Children.Add(GradientElement(gradient.Key, gradient.Value));
            if (defs.Children.Count > 0) svg.Children.Add(defs);
        }

        foreach (var shape in drawing.Artwork.Shapes)
        {
            var path = new RealizedElement("path");
            var attributes = new Dictionary<string, string> { ["d"] = shape.Path };
            attributes["fill"] = PaintCss(shape.Fill);
            // `currentColor` has no alpha of its own, so an inherited paint drawn at a fraction
            // says so beside it — a Solid one carries its alpha in the colour already.
            if (shape.Fill.Kind == VectorPaintKind.Inherit && shape.Fill.Alpha < 1)
                attributes["fill-opacity"] = TokenCss.Number(shape.Fill.Alpha);
            if (shape.Stroke.Paints)
            {
                attributes["stroke"] = PaintCss(shape.Stroke);
                if (shape.Stroke.Kind == VectorPaintKind.Inherit && shape.Stroke.Alpha < 1)
                    attributes["stroke-opacity"] = TokenCss.Number(shape.Stroke.Alpha);
                attributes["stroke-width"] = TokenCss.Number(shape.StrokeWidth);
                attributes["stroke-linecap"] = "round";
                attributes["stroke-linejoin"] = "round";
            }
            if (shape.EvenOdd) attributes["fill-rule"] = "evenodd";
            if (shape.Opacity < 1) attributes["opacity"] = TokenCss.Number(shape.Opacity);
            path.RawAttributes = attributes;
            svg.Children.Add(path);
        }
        return svg;
    }

    private static string PaintCss(VectorPaint paint) => paint.Kind switch
    {
        VectorPaintKind.Inherit => "currentColor",
        VectorPaintKind.Solid => CssColor(paint.Color),
        VectorPaintKind.LinearGradient or VectorPaintKind.RadialGradient when paint.Gradient is { } run
            => $"url(#{GradientId(run)})",
        _ => "none",
    };

    /// <summary>
    /// The runs a drawing paints with, keyed by the id the shapes will name. Ordered by first use
    /// so the markup reads in the order the artwork does, and deduplicated because the id IS the
    /// content: two shapes with the same run declare it once.
    /// </summary>
    private static Dictionary<string, VectorPaint> Gradients(VectorDrawing artwork)
    {
        var runs = new Dictionary<string, VectorPaint>(StringComparer.Ordinal);
        foreach (var shape in artwork.Shapes)
        {
            foreach (var paint in (VectorPaint[])[shape.Fill, shape.Stroke])
            {
                if (!paint.IsGradient) continue;
                runs.TryAdd(GradientId(paint.Gradient!.Value), paint);
            }
        }
        return runs;
    }

    /// <summary>
    /// A gradient's id: the FNV hash of everything that makes it what it is. Content-addressed
    /// rather than counted, for the reason every other generated id here is — a counter depends on
    /// how many drawings came before, and SSR and the client twin do not agree about that.
    /// <para>
    /// PUBLIC because it is a promise to the other producer rather than an implementation detail:
    /// the client twin computes the same string from the same run, and a cross-pin fixture holds
    /// the two to it.
    /// </para>
    /// </summary>
    public static string GradientId(VectorGradient run)
    {
        var text = new System.Text.StringBuilder();
        text.Append(run.UserSpace ? 'u' : 'o').Append('|')
            .Append(TokenCss.Number(run.X1)).Append(',').Append(TokenCss.Number(run.Y1)).Append(',')
            .Append(TokenCss.Number(run.X2)).Append(',').Append(TokenCss.Number(run.Y2)).Append(',')
            .Append(TokenCss.Number(run.Radius));
        foreach (var stop in run.Stops)
            text.Append('|').Append(TokenCss.Number(stop.Offset)).Append(':').Append(CssColor(stop.Color));
        return $"eq-g-{StyleAtomizer.Hash(text.ToString())}";
    }

    /// <summary>
    /// Every run the DOCUMENT paints with, in one container the whole page references. See
    /// <see cref="GradientSink"/> for why it stopped living inside each drawing.
    /// </summary>
    internal static HtmlElement GradientContainer(IReadOnlyDictionary<string, VectorPaint> runs)
    {
        var svg = new RealizedElement("svg")
        {
            Id = GradientSink.ContainerId,
            Style = new HtmlStyle
            {
                // Out of flow and zero-sized — NOT display:none, which is what makes a paint server
                // unusable and is the bug this replaces.
                Position = Core.Position.Absolute,
                Width = "0",
                Height = "0",
                Overflow = "hidden",
            },
            RawAttributes = new Dictionary<string, string> { ["aria-hidden"] = "true" },
        };

        var defs = new RealizedElement("defs");
        foreach (var run in runs) defs.Children.Add(GradientElement(run.Key, run.Value));
        svg.Children.Add(defs);
        return svg;
    }

    /// <summary>
    /// One paint server, in SVG's own words. The stop's alpha rides <c>stop-opacity</c> rather than
    /// an 8-digit colour: that is the spelling every renderer agrees on, and the one a designer sees
    /// when they open the file again.
    /// </summary>
    private static HtmlElement GradientElement(string id, VectorPaint paint)
    {
        var run = paint.Gradient!.Value;
        var radial = paint.Kind == VectorPaintKind.RadialGradient;
        var element = new RealizedElement(radial ? "radialGradient" : "linearGradient");
        var attributes = new Dictionary<string, string> { ["id"] = id };
        if (radial)
        {
            attributes["cx"] = TokenCss.Number(run.X1);
            attributes["cy"] = TokenCss.Number(run.Y1);
            attributes["r"] = TokenCss.Number(run.Radius);
        }
        else
        {
            attributes["x1"] = TokenCss.Number(run.X1);
            attributes["y1"] = TokenCss.Number(run.Y1);
            attributes["x2"] = TokenCss.Number(run.X2);
            attributes["y2"] = TokenCss.Number(run.Y2);
        }
        // The default is objectBoundingBox and SVG's own, so only the other one is written.
        if (run.UserSpace) attributes["gradientUnits"] = "userSpaceOnUse";
        element.RawAttributes = attributes;

        foreach (var stop in run.Stops)
        {
            var child = new RealizedElement("stop");
            var stopAttributes = new Dictionary<string, string>
            {
                ["offset"] = TokenCss.Number(stop.Offset),
                ["stop-color"] = CssColor(Primitives.Color.FromRgb(stop.Color.R, stop.Color.G, stop.Color.B)),
            };
            if (stop.Color.A < 255) stopAttributes["stop-opacity"] = TokenCss.Number(stop.Color.A / 255f);
            child.RawAttributes = stopAttributes;
            element.Children.Add(child);
        }
        return element;
    }

    /// <summary>
    /// A literal artwork colour, which is NOT a token: it is what the designer drew, and it does
    /// not move with the theme. Spelled `#rrggbb(aa)` because the client twin's own formatter is —
    /// the two lowerings have to agree byte for byte or hydration patches every path it touches.
    /// </summary>
    private static string CssColor(Primitives.Color color) =>
        color.A == 255
            ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
            : $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";


    private static HtmlElement LowerGlyph(IconGlyph glyph, float width, float height, ColorToken? color, string? label)
    {
        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                // Block, not inline: an inline svg sits on the TEXT BASELINE of whatever box
                // holds it, and inherits a line box — a 9dp arrowhead inside a positioned
                // wrapper rendered ~a descender below the line it was aimed at. Flex items
                // ignore display, so every icon in a Row is unaffected.
                Display = Core.Display.Block,
                Width = TokenCss.Px(width),
                Height = TokenCss.Px(height),
                Color = color is { } tint ? TokenCss.Value(tint) : null,
            },
        };
        svg.RawAttributes = new Dictionary<string, string>
        {
            ["viewBox"] = glyph.ViewBox,
        };
        // Fill glyphs are alpha masks; stroke glyphs are the outline family (2dp round — spec §07).
        if (glyph.Style == IconGlyphStyle.Stroke)
        {
            svg.RawAttributes["fill"] = "none";
            svg.RawAttributes["stroke"] = "currentColor";
            svg.RawAttributes["stroke-width"] = glyph.StrokeWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            svg.RawAttributes["stroke-linecap"] = "round";
            svg.RawAttributes["stroke-linejoin"] = "round";
        }
        else
        {
            svg.RawAttributes["fill"] = "currentColor";
        }
        if (label is { })
        {
            // A10: "with label → image role". An aria-label on a bare <svg> gives the node a NAME
            // and no role, so a screen reader announces the words and cannot say what they belong
            // to — some read it as a group, some as nothing. The native bridges have emitted
            // SemanticRole.Image for a labelled glyph since the a11y pass (Semantics.cs), so this was
            // also the web disagreeing with its own twin.
            svg.RawAttributes["aria-label"] = label;
            svg.RawAttributes["role"] = "img";
        }
        else svg.RawAttributes["aria-hidden"] = "true";

        var glyphPath = new RealizedElement("path");
        glyphPath.RawAttributes = new Dictionary<string, string> { ["d"] = glyph.Path };
        svg.Children.Add(glyphPath);
        return svg;
    }

    /// <summary>Spec A11 lowering: an explicitly sized <c>&lt;img&gt;</c> with object-fit and the
    /// rrect clip via border-radius; empty alt = decorative (HTML semantics).</summary>
    /// <summary>
    /// Phase C viewport layer: a generated fixed inset-0 stacking layer (.eq-overlay) — the child
    /// owns its composition (scrim, centering) from the ordinary vocabulary. Fixed positioning
    /// escapes the page flow visually without a portal; keep Overlays out of transformed subtrees
    /// (LoopMotion) — CSS transforms re-anchor fixed descendants.
    /// </summary>
    /// <summary>
    /// Spec S8: the binding is MARKED on the child's root (<c>data-eq-shortcut</c>) and the runtime's
    /// window controller dispatches to it — SSR has no key events, so the marker is the whole
    /// server-side contract (and it keeps the SSR/hydration DOM identical to the TS lowering).
    /// </summary>
    private static HtmlElement? LowerShortcut(Shortcut shortcut, ComponentContext context, bool? horizontalAxis)
    {
        if (LowerNode(shortcut.Child, context, horizontalAxis) is not { } child) return null;
        if (child is RealizedElement realized)
        {
            realized.RawAttributes ??= new Dictionary<string, string>();
            // NESTED shortcuts share one child root (a dialog binds Esc, ↑, ↓ and ↵ around the same
            // subtree) — the marker lists them all rather than the outermost overwriting the rest.
            var chord = ChordId(shortcut.Chord);
            realized.RawAttributes["data-eq-shortcut"] =
                realized.RawAttributes.TryGetValue("data-eq-shortcut", out var existing) && existing.Length > 0
                    ? $"{existing} {chord}"
                    : chord;
        }
        return child;
    }

    /// <summary>The chord's wire form — <c>command+shift+k</c>, modifiers in a FIXED order so both
    /// realizers and the runtime controller compare the same string.</summary>
    internal static string ChordId(KeyChord chord)
    {
        var parts = new List<string>(4);
        if (chord.Modifiers.HasFlag(KeyModifiers.Command)) parts.Add("command");
        if (chord.Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("control");
        if (chord.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("alt");
        if (chord.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("shift");
        parts.Add(chord.Key.ToLowerInvariant());
        return string.Join("+", parts);
    }

    private static HtmlElement LowerOverlay(Overlay overlay, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            ClassName = overlay.Modal ? "eq-overlay" : "eq-overlay eq-overlay-passthrough",
        };
        // §10 dialog semantics + the trap marker (TS twin: lowerOverlay). A modal, OPEN layer is a
        // dialog: aria-modal is what tells assistive tech the page behind is inert, tabindex=-1
        // makes the container the initial-focus fallback, and data-eq-trap is the marker the
        // client's controller discovers after each pass — SSR emits no listeners, exactly as
        // Pressable's click and Shortcut's chord don't. A toast layer is none of this, and a
        // closed one is not a dialog right now.
        if (overlay.Modal && overlay.Open)
        {
            // alertdialog is the assertive sibling — a destructive confirm interrupts; everything
            // else about the layer is identical. The label is the dialog's NAME: without one a
            // screen reader says a wall appeared but not which.
            element.Role = overlay.Alert ? "alertdialog" : "dialog";
            element.AriaModal = true;
            element.AriaLabel = overlay.Label;
            element.TabIndex = -1;
            element.RawAttributes = new Dictionary<string, string> { ["data-eq-trap"] = "" };
        }
        if (overlay.Motion is { } motion)
        {
            // Keep-mounted open/close (the Anchored.Motion twin): the layer fades both ways, and a
            // closed one is hidden — gone from hit-testing and the focus order.
            element.Style = new HtmlStyle { Transition = MotionTransition(motion, withTransform: false) };
            if (!overlay.Open)
            {
                element.Style.Opacity = "0";
                element.Style.Visibility = "hidden";
                element.Style.PointerEvents = "none";
            }
        }
        if (LowerNode(overlay.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Spec B9/B10 primitive: a REAL chrome-less &lt;input&gt; — the browser owns caret/selection/
    /// IME. The type role rides the generated .eq-type-* class; color/reset styles are inline;
    /// outline and ::placeholder mechanics live in the generated stylesheet (.eq-entry). The
    /// composing component owns the container chrome. Handlers attach on the client (lowering.ts —
    /// SSR emits no events; hydration wires them). The entry lowers inside a STABLE
    /// .eq-field shell that always carries the sr-only description twin (.eq-desc) — presence
    /// flipping with the description would REPLACE the input and drop focus mid-edit.
    /// </summary>
    private static HtmlElement LowerTextEntry(TextEntry entry, ComponentContext context)
    {
        // A multi-line entry IS a textarea — its value is CONTENT, not an attribute, so SSR carries
        // it the way the parser expects; `rows` is the authored line count.
        var multiline = entry.Lines > 1;
        var element = new RealizedElement(multiline ? "textarea" : "input")
        {
            ClassName = $"eq-entry eq-type-{entry.Role.ToString().ToLowerInvariant()}",
            Style = new HtmlStyle
            {
                // A field takes the keyboard and the pointer, and says so: `none` inherits, and
                // any row above it may now be transparent.
                PointerEvents = "auto",
                Width = "100%",
                Padding = "0",
                Background = "none",
                Border = "none",
                Color = TokenCss.Value(context.Theme.TextPrimary),
                FontFamily = "inherit",
                Resize = multiline ? "vertical" : null,
            },
            RawAttributes = multiline
                ? new Dictionary<string, string> { ["rows"] = entry.Lines.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                : new Dictionary<string, string>
                {
                    ["type"] = entry.Obscure ? "password" : "text",
                    ["value"] = entry.Value,
                },
        };
        if (multiline) element.InnerHtml = entry.Value;
        if (entry.Placeholder is { } placeholder) element.RawAttributes["placeholder"] = placeholder;
        // The accessible NAME — a placeholder vanishes under text, so it never substitutes for one.
        if (entry.Label is { Length: > 0 } accessibleName) element.RawAttributes["aria-label"] = accessibleName;
        if (entry.Disabled) element.RawAttributes["disabled"] = "";
        // NOT DECLARED HERE, and it is a gap rather than a decision: the client twin installs an
        // `input` handler and this side installs nothing, so the parity fixture — which is
        // generated from THIS tree — cannot notice the client losing one. Exactly the hole the
        // grid's keydown had before #12, except that one could be closed in an afternoon because
        // `OnKeyDown` already existed on HtmlElement. `OnInput` is in the event-name map with no
        // property behind it, so closing this means a Core API addition and a hydration story for
        // the value it carries. Filed, not smuggled into a component change.
        // The attribute alone only acts on the initial parse; the client lowering also focuses on
        // mount, which is what a dialog opened later needs.
        if (entry.Autofocus) element.RawAttributes["autofocus"] = "";
        if (entry.Invalid) element.RawAttributes["aria-invalid"] = "true";

        // The description twin: sr-only, and a polite live region — present (empty) from the first
        // paint so a later error swap ANNOUNCES instead of merely recoloring. The id is the
        // content's FNV hash, the atomizer's identity rule — deterministic across SSR and client,
        // no per-render counter (identical captions share one id; the referenced text is the same).
        var description = new RealizedElement("span")
        {
            ClassName = "eq-desc",
            RawAttributes = new Dictionary<string, string> { ["aria-live"] = "polite" },
        };
        if (entry.Description is { Length: > 0 } caption)
        {
            var descriptionId = $"eq-desc-{StyleAtomizer.Hash(caption)}";
            description.RawAttributes["id"] = descriptionId;
            description.InnerHtml = caption;
            element.RawAttributes["aria-describedby"] = descriptionId;
        }

        var host = new RealizedElement("div") { ClassName = "eq-field" };
        host.Children.Add(element);
        host.Children.Add(description);
        return host;
    }

    /// <summary>
    /// The live surface (TS twin: lowerCameraPreview). No session — which is every SSR, since a
    /// stream only ever exists client-side — renders the SurfaceSubtle placeholder div both
    /// realizers agree on; with one, the muted autoplaying video the runtime wires by session id.
    /// </summary>
    /// <summary>
    /// The TS twin's SSR half: same markup, no handler — keydown only exists client-side, exactly
    /// as Pressable's click does. One focusable wrapper is the control's whole Tab presence.
    /// </summary>
    private static HtmlElement LowerAdjustable(Adjustable adjustable, ComponentContext context)
    {
        var adjustableFills = Fills(adjustable.Child);
        var adjustableCap = CapsAt(adjustable.Child);
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = adjustableFills.Width ? "100%" : null,
                MaxWidth = Size(adjustableCap),
                PointerEvents = "auto",
                // BOTH axes, like every other wrapper and like the twin already did. Taking the
                // width and leaving the height is the same half-contract that made the Link
                // diverge, mirrored: there the twin was short, here this side was.
                Height = adjustableFills.Height ? "100%" : null,
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["role"] = adjustable.Role switch
                {
                    AdjustableRole.Tablist => "tablist",
                    AdjustableRole.Radiogroup => "radiogroup",
                    _ => "slider",
                },
                ["tabindex"] = "0",
            },
        };
        if (adjustable.Label is { Length: > 0 } label) element.RawAttributes["aria-label"] = label;
        if (LowerNode(adjustable.Child, context, null) is { } child) element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// The 2-D composite (TS twin: lowerNavigable). One focusable host carrying the grid role and
    /// the name, and one ROW per declared row — <c>display:contents</c>, so the row exists for
    /// assistive tech and not for layout: the caller's Grid or Column keeps laying the cells out
    /// exactly as it did. A grid whose cells are not inside rows is an invalid tree, which is why
    /// the rows are structural here rather than left to the caller's markup.
    /// <para>SSR half: the same markup, no handler — keydown only exists client-side, exactly as
    /// Pressable's click does.</para>
    /// </summary>
    private static HtmlElement LowerNavigable(Navigable navigable, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            RawAttributes = new Dictionary<string, string>
            {
                ["role"] = navigable.Role switch { _ => "grid" },
                ["tabindex"] = "0",
            },
        };
        if (navigable.Label is { Length: > 0 } gridLabel) element.RawAttributes["aria-label"] = gridLabel;
        // The keyboard, DECLARED here as well as installed by the client twin. The server never
        // dispatches a key, so this delegate does not run — but the lowered tree is what the
        // parity net compares, and a fixture that says "no handler" cannot notice the client
        // losing one. NavigableKeys is the same table the TS half reads, cross-pinned.
        if (navigable.OnMove is { } onMove)
            element.OnKeyDown = key =>
            {
                if (NavigableKeys.Move(key.Key, key.ShiftKey) is { } move) onMove(move);
            };
        // Ids are scoped to THIS grid: two calendars on one page would otherwise both call their
        // cells eq-cell-1-1, which is a duplicate id and an activedescendant that may resolve into
        // the wrong grid. The scope is the grid's own name, hashed with the atomizer's FNV — the
        // one hash both producers compute identically, which is what SSR hydration needs.
        var scope = NavigableScope(navigable);
        if (navigable.ActiveCell is { } active)
            element.RawAttributes["aria-activedescendant"] = NavigableCellId(scope, active.Row, active.Item);

        for (var index = 0; index < navigable.Rows.Count; index++)
        {
            var row = new RealizedElement("div")
            {
                // Transparent to layout: the row is an accessibility fact, not a box.
                Style = new HtmlStyle { Display = Core.Display.Contents },
                RawAttributes = new Dictionary<string, string> { ["role"] = "row" },
            };
            if (LowerNode(navigable.Rows[index], context, null) is { } lowered)
            {
                row.Children.Add(lowered);
                if (navigable.HasHeaderRow && index == 0) MarkColumnHeaders(lowered);
            }
            // The cells are IDENTIFIED — an aria-activedescendant pointing at an id nothing
            // carries is a dangling reference, which reads to assistive tech as no focus at all.
            var item = 0;
            if (!(navigable.HasHeaderRow && index == 0)) NumberGridCells(row, scope, index, ref item);
            element.Children.Add(row);
        }
        return element;
    }

    /// <summary>Walks one lowered row and ids every gridcell in tree order — the id the host's
    /// aria-activedescendant points at, built the same way by the TS twin.</summary>
    private static void NumberGridCells(Core.HtmlElement element, string scope, int row, ref int item)
    {
        if (element.Role == "gridcell")
        {
            element.Id = NavigableCellId(scope, row, item);
            item++;
        }
        foreach (var child in element.Children)
            if (child is Core.HtmlElement childElement)
                NumberGridCells(childElement, scope, row, ref item);
    }

    /// <summary>The header row's cells NAME their columns (design system C15: the day names).
    /// They are the row content's DIRECT children — whatever the caller laid out, one element per
    /// column — so the rule is structural rather than a guess about what a header looks like.</summary>
    private static void MarkColumnHeaders(Core.HtmlElement rowContent)
    {
        foreach (var child in rowContent.Children)
            if (child is Core.HtmlElement cell)
                cell.Role = "columnheader";
    }

    /// <summary>The id a cell answers to for <c>aria-activedescendant</c> — the TS twin builds the
    /// same string, or the attribute points at nothing.</summary>
    internal static string NavigableCellId(string scope, int row, int item) =>
        $"eq-cell-{scope}-{row}-{item}";

    /// <summary>What makes one grid's cell ids distinct from another's on the same page: the
    /// grid's NAME, hashed. Two grids that also share a name (the same month rendered twice) would
    /// still collide — a documented limit, and the point where a caller should give them names
    /// that differ, which a screen reader needs anyway.</summary>
    internal static string NavigableScope(Navigable navigable) =>
        StyleAtomizer.Hash(navigable.Label);

    private static HtmlElement LowerCameraPreview(CameraPreview camera)
    {
        var style = new HtmlStyle
        {
            Width = TokenCss.Px(camera.Width),
            Height = TokenCss.Px(camera.Height),
            ObjectFit = "cover",
            Background = "var(--eq-color-surface-subtle)",
            BorderRadius = camera.CornerRadius.IsZero ? null : TokenCss.Radius(camera.CornerRadius),
        };
        if (camera.Session is null) return new RealizedElement("div") { Style = style };
        return new RealizedElement("video")
        {
            Style = style,
            RawAttributes = new Dictionary<string, string>
            {
                ["data-eq-camera"] = camera.Session.Id,
                ["autoplay"] = "",
                ["muted"] = "",
                ["playsinline"] = "",
                ["aria-label"] = camera.Alt,
            },
        };
    }

    /// <summary>
    /// A sandboxed <c>iframe</c>. <c>sandbox</c> is ALWAYS present — an empty value is the locked
    /// frame, and each <see cref="WebSandbox"/> flag appends its <c>allow-</c> token. Inline
    /// <see cref="WebFrame.Document"/> wins over <see cref="WebFrame.Source"/>; the border is the
    /// frame's own 1990s default, so it goes.
    /// </summary>
    private static HtmlElement LowerWebFrame(WebFrame frame)
    {
        var tokens = new List<string>(4);
        if (frame.Sandbox.HasFlag(WebSandbox.Scripts)) tokens.Add("allow-scripts");
        if (frame.Sandbox.HasFlag(WebSandbox.SameOrigin)) tokens.Add("allow-same-origin");
        if (frame.Sandbox.HasFlag(WebSandbox.Forms)) tokens.Add("allow-forms");
        if (frame.Sandbox.HasFlag(WebSandbox.Popups)) tokens.Add("allow-popups");

        var attributes = new Dictionary<string, string>
        {
            ["sandbox"] = string.Join(' ', tokens),
            ["title"] = frame.Title,
        };
        if (frame.Document is { Length: > 0 } document) attributes["srcdoc"] = document;
        else if (frame.Source is { Length: > 0 } source) attributes["src"] = source;

        return new RealizedElement("iframe")
        {
            Style = new HtmlStyle
            {
                Width = SizeCss(frame.Width),
                Height = SizeCss(frame.Height),
                Border = "0",
                Display = Display.Block,
                BorderRadius = frame.CornerRadius.IsZero ? null : TokenCss.Radius(frame.CornerRadius),
            },
            RawAttributes = attributes,
        };

        static string? SizeCss(SizeValue size) => size.Kind switch
        {
            SizeKind.Fixed => TokenCss.Px(size.Value),
            SizeKind.Fill => "100%",
            _ => null, // hug = the element's own default
        };
    }

    private static HtmlElement LowerImage(Primitives.Image image)
    {
        var element = new RealizedElement("img")
        {
            Style = new HtmlStyle
            {
                Width = TokenCss.Px(image.Width),
                Height = TokenCss.Px(image.Height),
                ObjectFit = image.Fit switch
                {
                    ImageFit.Contain => "contain",
                    ImageFit.Stretch => "fill",
                    _ => "cover",
                },
                BorderRadius = image.CornerRadius.IsZero ? null : TokenCss.Radius(image.CornerRadius),
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["src"] = image.Source,
                ["alt"] = image.Alt,
            },
        };
        if (image.DarkSource is not { Length: > 0 } darkSource) return element;

        // A PAIR of artworks, resolved the way a ColorToken is: both ship, CSS shows one. The
        // generated .eq-themed-* rules follow the OS preference AND the app's own forced mode
        // (the theme controller stamps data-theme), so nothing decides this at paint time.
        element.ClassName = "eq-themed-light";
        var dark = new RealizedElement("img")
        {
            ClassName = "eq-themed-dark",
            Style = element.Style,
            RawAttributes = new Dictionary<string, string>
            {
                ["src"] = darkSource,
                ["alt"] = image.Alt,
            },
        };
        var pair = new RealizedElement("span")
        {
            Style = new HtmlStyle { Display = Display.Contents },
        };
        pair.Children.Add(element);
        pair.Children.Add(dark);
        return pair;
    }

    /// <summary>
    /// A box that paints NOTHING and holds nothing is the vocabulary's way of saying "draw nothing"
    /// — `if (!Open) return new Box();` in Drawer, `if (field is null) return new Box();` in
    /// FormInput, and the same line in the docs for a capability a target does not have.
    /// <para>
    /// Saying it must also mean "intercept nothing". Inside a Stack the element is stretched to the
    /// whole cell and given the layer's z-index, so a CLOSED drawer covered the viewport with an
    /// invisible, fully interactive rectangle: on a phone the shell's own menu button could not be
    /// tapped, and every tap landed on a layer that was not there. Programmatic clicks in tests hit
    /// the element directly, which is why nothing noticed.
    /// </para>
    /// <para>
    /// The ELEMENT stays: removing it would change child counts, and the pressed/focus mechanics
    /// select `> :first-child`. Only its ability to be a target goes.
    /// </para>
    /// </summary>
    private static bool PaintsNothing(Box box) =>
        box.Child is null
        && box.Style.Background is null
        && box.Style.Gradient is null
        && box.Style.Glow is null
        && box.Style.Pattern is null
        && box.Style.BorderWidth == 0
        && box.Style.Elevation == 0;

    private static HtmlElement LowerBox(Box box, ComponentContext context)
    {
        var style = box.Style;
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // Photon borders draw INSIDE the bounds — border-box is the CSS-parity contract.
                BoxSizing = "border-box",
                Width = Size(style.Width),
                Height = Size(style.Height, vertical: true),
                FlexShrink = Rigid(style.Width, style.Height),
                // FILL is a CEILING: a flex/grid item's automatic minimum size overrides width,
                // so a Fill box grew past its parent to fit its longest content. An explicit
                // MinWidth still wins — the author asked for it.
                MinWidth = style.MinWidth > 0 ? TokenCss.Px(style.MinWidth)
                    : style.Width.Kind == SizeKind.Fill ? "0" : null,
                MinHeight = style.MinHeight > 0 ? TokenCss.Px(style.MinHeight)
                    : style.Height.Kind == SizeKind.Fill ? "0" : null,
                // The caps go through the same axis-aware translation as the sizes: Hug is
                // unbounded (null), a number is dp, and the window kind becomes a `calc` the
                // browser resolves per window.
                MaxWidth = Size(style.MaxWidth),
                MaxHeight = Size(style.MaxHeight, vertical: true),
                Padding = style.Padding == EdgeInsets.Zero ? null : TokenCss.Padding(style.Padding),
                BackgroundColor = style.Background is { } bg ? TokenCss.Value(bg) : null,
                // Layers compose like CSS: the FIRST entry paints on top, so a gradient sits over
                // the grid, and both sit over background-color. background-size must carry one
                // entry per layer, hence the `auto` placeholder for the gradient.
                BackgroundImage = BackgroundLayers(style),
                BackgroundSize = BackgroundLayerSizes(style),
                BorderRadius = style.CornerRadius.IsZero ? null : TokenCss.Radius(style.CornerRadius),
                // Depth (Elevation), the CUSTOM shadow and the inset highlight compose as one
                // box-shadow list — the design stacks glows on elevated glossy cards.
                BoxShadow = ComposeShadows(
                    style.Elevation > 0 && !context.Theme.Elevation(style.Elevation).IsNone
                        ? TokenCss.Shadow(context.Theme.Elevation(style.Elevation))
                        : null,
                    style.Shadow is { } shadow ? TokenCss.Shadow(shadow) : null,
                    style.Shadows is { Count: > 0 } shadows
                        ? string.Join(", ", shadows.Select(TokenCss.Shadow))
                        : null,
                    style.InsetHighlight is { } inset ? $"inset 0 1px 0 {TokenCss.Value(inset)}" : null),
                // The shorthand for a full border (one declaration, one atomic class); per-side
                // widths when the box draws only some edges — a section rule, an accent bar, a
                // table cell sharing its neighbour's line.
                Border = style.BorderWidth > 0 && style.BorderSides == BorderSides.All
                    ? $"{TokenCss.Px(style.BorderWidth)} solid {TokenCss.Value(style.BorderColor)}"
                    : null,
                BorderWidth = style.BorderWidth > 0 && style.BorderSides != BorderSides.All
                    ? SideWidths(style.BorderWidth, style.BorderSides)
                    : null,
                BorderStyle = style.BorderWidth > 0 && style.BorderSides is not (BorderSides.All or BorderSides.None)
                    ? "solid"
                    : null,
                BorderColor = style.BorderWidth > 0 && style.BorderSides is not (BorderSides.All or BorderSides.None)
                    ? TokenCss.Value(style.BorderColor)
                    : null,
                // The container side of loop motion: children clip to the rrect (native PushClip twin).
                Overflow = style.Clip ? "hidden" : null,
                // Spec S1 — group opacity (native PushLayer twin), the center-anchored static
                // transform (paint-only), and the one-axis-derives aspect ratio.
                Opacity = style.Opacity is { } alpha && alpha < 1f ? TokenCss.Number(alpha) : null,
                // Spec S3 frosted glass (native BackdropBlur pass-split twin).
                BackdropFilter = style.BackdropBlur > 0 ? $"blur({TokenCss.Px(style.BackdropBlur)})" : null,
                // ELEMENT blur (blur-3xl glow washes) — this box's own pixels.
                Filter = style.Blur > 0 ? $"blur({TokenCss.Px(style.Blur)})" : null,
                Transform = style.Transform is { } transform ? TokenCss.Transform(transform) : null,
                AspectRatio = style.AspectRatio > 0 ? TokenCss.Number(style.AspectRatio) : null,
                // Spec S6 — changes to the covered channels GLIDE (hover diffs, the scrolled
                // variant, re-rendered values). Native fence: the style interpolator (snap).
                Transition = style.Transition is { } transition ? TokenCss.Transition(transition) : null,
                // The CSS cursor mirror (Photon twin: a cursor region the host answers from).
                Cursor = style.Cursor != PointerCursor.Default ? TokenCss.Cursor(style.Cursor) : null,
                // "Draw nothing" means intercept nothing — see PaintsNothing. And the other
                // half, which the transparent-layout rule makes necessary: a box that DOES paint
                // has to say it is a target, because `none` inherits and any ancestor row may now
                // be carrying it. Targets declare themselves; everything else is passed through.
                PointerEvents = PaintsNothing(box) ? "none" : "auto",
            },
        };

        // ELEVATION DECIDES WHAT IS ON TOP, not just how deep the shadow is.
        //
        // It used to draw a shadow and nothing else, which is half a sentence: a raised surface that
        // anything drawn after it covers is not raised. The case that names it is chrome that pins
        // while the page scrolls — content passes over the header, and the only cures on offer were
        // a z-index on the node (CSS vocabulary, which the abstract layer does not speak) or moving
        // the chrome into a layer of its own (which is what Overlay is, and a sticky header is not).
        // Elevation is the word the design system already has for "above".
        if (style.Elevation > 0)
        {
            element.Style!.ZIndex = style.Elevation.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // z-index needs a positioned box, and `relative` is the one that changes nothing else.
            // A box that already positions itself — sticky chrome, an absolute anchor — keeps its own.
            element.Style.Position ??= Core.Position.Relative;
        }

        // A SIMULATED state REPLACES the base declarations; otherwise the diff rides the
        // pseudo-class it belongs to. Replacing rather than adding a second class of the same
        // property matters: atomic classes all have equal specificity, so two of them on one
        // element are decided by stylesheet insertion order — which depends on what the rest of
        // the page happened to emit first.
        var simulated = _simulated.Value;
        if (box.Style.Hover is { IsEmpty: false } hover)
        {
            if (simulated.HasFlag(SimulatedState.Hovered)) ApplyDiff(element.Style, hover, context);
            else AppendDiff(element, ":hover", hover, context);
        }
        if (box.Style.Focus is { IsEmpty: false } focus)
        {
            if (simulated.HasFlag(SimulatedState.Focused)) ApplyDiff(element.Style, focus, context);
            else AppendDiff(element, ":focus-visible", focus, context);
        }

        if (box.Child is not null && LowerNode(box.Child, context, horizontalAxis: null) is { } child)
        {
            // A box with a DECIDED height hands that height to an auto-sized container child — the
            // native twin of this is the block stretch the layout engine applies on both axes. CSS
            // gives it for free on the width (a div's child div is full-width) and nothing on the
            // height, so the height is said out loud.
            //
            // `100%` rather than a number because CSS then answers the indeterminate case the same
            // way the layout does: inside a parent whose own height is auto, a percentage height
            // computes to auto, and the child hugs exactly as it did before.
            if (StretchesChildHeight(style, box.Child))
            {
                child.Style ??= new HtmlStyle();
                child.Style.Height ??= "100%";
            }
            element.Children.Add(child);
        }
        return element;
    }

    /// <summary>
    /// Whether this box hands its height down: the box decided one, and the child is an auto-sized
    /// CONTAINER. Text, images and icons size themselves; a button, a link or an input hugs, which
    /// is the inline-block fence the width stretch has always respected.
    /// </summary>
    private static bool StretchesChildHeight(BoxStyle style, VisualNode child)
    {
        if (style.Height.Kind == SizeKind.Hug) return false;
        return child switch
        {
            Box inner => inner.Style.Height.Kind == SizeKind.Hug,
            FlexNode flex => flex.Height.Kind == SizeKind.Hug,
            _ => false,
        };
    }

    /// <summary>One box-shadow list from the optional parts (null when none are set).</summary>
    /// <summary>
    /// <c>border-width</c> in CSS's own order — top, right, bottom, left — from the logical sides.
    /// Start/End map to left/right here because this is the LTR realization; a right-to-left
    /// document flips them in the same place every other logical property is flipped.
    /// </summary>
    private static string SideWidths(float width, BorderSides sides)
    {
        var w = TokenCss.Px(width);
        string Edge(BorderSides side) => sides.HasFlag(side) ? w : "0";
        return $"{Edge(BorderSides.Top)} {Edge(BorderSides.End)} {Edge(BorderSides.Bottom)} {Edge(BorderSides.Start)}";
    }

    private static string? ComposeShadows(params string?[] parts)
    {
        var present = parts.Where(part => part != null).ToList();
        return present.Count == 0 ? null : string.Join(", ", present);
    }

    /// <summary>
    /// The same members, written over the BASE style — what a simulated state does. The pairs here
    /// mirror <see cref="AppendDiff"/> exactly, so a preview shows the declarations a real hover
    /// would produce rather than an approximation of them.
    /// </summary>
    private static void ApplyDiff(HtmlStyle? style, in StyleDiff diff, ComponentContext context)
    {
        if (style is null) return;
        if (diff.Background is { } bg) style.BackgroundColor = TokenCss.Value(bg);
        if (diff is { BorderWidth: { } bw, BorderColor: { } bc })
            style.Border = $"{TokenCss.Px(bw)} solid {TokenCss.Value(bc)}";
        else if (diff.BorderColor is { } onlyColor) style.BorderColor = TokenCss.Value(onlyColor);
        if (diff.Elevation is { } level && !context.Theme.Elevation(level).IsNone)
            style.BoxShadow = TokenCss.Shadow(context.Theme.Elevation(level));
        if (diff.Opacity is { } alpha) style.Opacity = TokenCss.Number(alpha);
        if (diff.Gradient is { } gradient) style.BackgroundImage = TokenCss.Gradient(gradient);
    }

    /// <summary>Spec S5: a StyleDiff's set members as pseudo-state declarations (base values keep).</summary>
    private static void AppendDiff(RealizedElement element, string pseudo, in StyleDiff diff, ComponentContext context)
    {
        if (diff.Background is { } bg)
            element.PseudoDeclarations.Add((pseudo, "background-color", TokenCss.Value(bg)));
        if (diff is { BorderWidth: { } bw, BorderColor: { } bc })
            element.PseudoDeclarations.Add((pseudo, "border", $"{TokenCss.Px(bw)} solid {TokenCss.Value(bc)}"));
        else if (diff.BorderColor is { } onlyColor)
            element.PseudoDeclarations.Add((pseudo, "border-color", TokenCss.Value(onlyColor)));
        if (diff.Elevation is { } level && !context.Theme.Elevation(level).IsNone)
            element.PseudoDeclarations.Add((pseudo, "box-shadow", TokenCss.Shadow(context.Theme.Elevation(level))));
        if (diff.Opacity is { } alpha)
            element.PseudoDeclarations.Add((pseudo, "opacity", TokenCss.Number(alpha)));
        if (diff.Gradient is { } gradient)
            element.PseudoDeclarations.Add((pseudo, "background-image", TokenCss.Gradient(gradient)));
    }

    /// <summary>
    /// Spec §06 loop motion: a layout-transparent div carrying the GENERATED keyframe animation —
    /// the effect maps to a keyframe name, endpoints ride custom properties at the style tail
    /// (fractions of the element's own width — CSS translateX(%) has the same base as the native
    /// realizer's offset math), duration rides the animation shorthand. `prefers-reduced-motion`
    /// disables it in the generated stylesheet (the .eq-loop class is the hook).
    /// </summary>
    private static HtmlElement LowerLoopMotion(LoopMotion motion, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            // Decorative loops additionally hide under prefers-reduced-motion (generated rule).
            ClassName = motion.HideAtRest ? "eq-loop eq-loop-rest-hidden" : "eq-loop",
            Style = new HtmlStyle
            {
                Animation = $"eq-slide-x {motion.DurationMs}ms linear infinite",
                CustomProperties = new Dictionary<string, string>
                {
                    ["--eq-loop-from"] = TokenCss.Percent(motion.FromX),
                    ["--eq-loop-to"] = TokenCss.Percent(motion.ToX),
                },
            },
        };
        if (LowerNode(motion.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    private static HtmlElement LowerFlex(FlexNode flex, ComponentContext context)
    {
        var horizontal = flex is Row;
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                BoxSizing = "border-box",
                Display = Display.Flex,
                FlexDirection = horizontal ? FlexDirection.Row : FlexDirection.Column,
                // Spec S3 wrap: CSS gap is "row-gap column-gap" — the RUN gap rides the axis the
                // lines stack on (rows stack vertically for a Row, horizontally for a Column).
                FlexWrap = flex.Wrap ? FlexWrap.Wrap : null,
                Gap = GapValue(flex, horizontal),
                JustifyContent = flex.Main switch
                {
                    MainAlign.Center => JustifyContent.Center,
                    MainAlign.End => JustifyContent.FlexEnd,
                    MainAlign.SpaceBetween => JustifyContent.SpaceBetween,
                    _ => JustifyContent.FlexStart,
                },
                AlignItems = flex.Cross switch
                {
                    CrossAlign.Start => AlignItem.FlexStart,
                    CrossAlign.Center => AlignItem.Center,
                    CrossAlign.End => AlignItem.FlexEnd,
                    _ => AlignItem.Stretch,
                },
                Width = Size(flex.Width),
                Height = Size(flex.Height, vertical: true),
                FlexShrink = Rigid(flex.Width, flex.Height),
                // FILL means "the parent's extent, never more" — and a flex ITEM's automatic
                // minimum size OVERRIDES width, so one long line of content grew a Fill row past
                // its parent (the playground's panes laid out at 3134px inside a 1440px page).
                // min-width/min-height 0 is what makes Fill mean what it says on the axis it fills.
                MinWidth = flex.Width.Kind == SizeKind.Fill ? "0" : null,
                MinHeight = flex.Height.Kind == SizeKind.Fill ? "0" : null,
                Padding = flex.Padding == EdgeInsets.Zero ? null : TokenCss.Padding(flex.Padding),
                BackgroundColor = flex.Background is { } bg ? TokenCss.Value(bg) : null,
                // A layout node that PAINTS NOTHING is not a hit target — the framework's
                // inspiration decides it this way, and the two runtimes disagree by default:
                // Flutter's RenderBox.hitTestSelf answers false, so a Row is invisible to a tap
                // and only its children register, while every DOM element is a target whether it
                // draws or not. Following the DOM here made a full-width transparent band swallow
                // clicks meant for what is behind it — the reason a page had to reach for
                // `pointer-events` by hand and could not, because the vocabulary has no such word
                // and must not: it has to mean the same on a target with no CSS at all.
                PointerEvents = flex.Background is null ? "none" : "auto",
                BorderRadius = flex.CornerRadius.IsZero ? null : TokenCss.Radius(flex.CornerRadius),
            },
        };

        foreach (var child in flex.Children)
        {
            if (LowerNode(child, context, horizontal) is { } lowered)
            {
                // Spec S1 align-self: the child overrides the container's Cross for itself.
                if (child.AlignSelf is { } self)
                {
                    lowered.Style ??= new HtmlStyle();
                    lowered.Style.AlignSelf = self switch
                    {
                        CrossAlign.Start => "flex-start",
                        CrossAlign.Center => "center",
                        CrossAlign.End => "flex-end",
                        _ => "stretch",
                    };
                }
                element.Children.Add(lowered);
            }
        }
        return element;
    }

    /// <summary>Spec S6: every DECLARED variant renders, each inside a gate whose fixed media rules
    /// show it only in its size-class range (display:contents keeps gates transparent to flex/grid).
    /// The ranges encode the same fallback chain the native Resolve uses — zero JS, zero listeners.</summary>
    private static HtmlElement LowerAdaptive(AdaptiveNode adaptive, ComponentContext context)
    {
        var wrapper = new RealizedElement("div") { Style = new HtmlStyle { Display = Display.Contents } };

        void AddVariant(VisualNode variant, string gate)
        {
            if (LowerNode(variant, context, horizontalAxis: null) is not { } lowered) return;
            var gated = new RealizedElement("div") { AdaptiveGate = gate };
            gated.Children.Add(lowered);
            wrapper.Children.Add(gated);
        }

        // A lone Compact needs no gating — it IS the tree at every size.
        if (adaptive.Medium is null && adaptive.Expanded is null)
            return LowerNode(adaptive.Compact, context, horizontalAxis: null) ?? wrapper;

        // Ranges chain off the DECLARED variants and the node's own thresholds (a design that
        // switches at 1024 gets 1024 gates, not the spec's 600/840).
        AddVariant(adaptive.Compact, AdaptiveGates.CompactUntil(
            adaptive.Medium is not null ? adaptive.MediumFrom : adaptive.ExpandedFrom));
        if (adaptive.Medium is { } medium)
            AddVariant(medium, AdaptiveGates.MediumFrom(
                adaptive.MediumFrom, adaptive.Expanded is not null ? adaptive.ExpandedFrom : 0));
        if (adaptive.Expanded is { } expanded)
            AddVariant(expanded, AdaptiveGates.ExpandedFrom(adaptive.ExpandedFrom));
        return wrapper;
    }

    /// <summary>Spec S4: CSS Grid — tracks as "px | Nfr | auto", the gap pair, spans per child.</summary>
    private static HtmlElement LowerGrid(Grid grid, ComponentContext context)
    {
        var tracks = string.Join(" ", grid.Columns.Select(t => t.Kind switch
        {
            SizeKind.Fixed => TokenCss.Px(t.Value),
            SizeKind.Fill => $"{TokenCss.Number(t.Value)}fr",
            _ => "auto",
        }));
        var rowGap = grid.RowGap ?? grid.Gap;
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                BoxSizing = "border-box",
                Display = Display.Grid,
                GridTemplateColumns = tracks,
                Gap = rowGap != grid.Gap ? $"{TokenCss.Px(rowGap)} {TokenCss.Px(grid.Gap)}"
                    : grid.Gap > 0 ? TokenCss.Px(grid.Gap) : null,
                Width = Size(grid.Width),
                Height = Size(grid.Height, vertical: true),
                FlexShrink = Rigid(grid.Width, grid.Height),
                Padding = grid.Padding == EdgeInsets.Zero ? null : TokenCss.Padding(grid.Padding),
            },
        };
        foreach (var child in grid.Children)
        {
            if (LowerNode(child, context, horizontalAxis: null) is { } lowered)
            {
                if (child.GridSpan > 1)
                {
                    lowered.Style ??= new HtmlStyle();
                    lowered.Style.GridColumn = $"span {child.GridSpan}";
                }
                element.Children.Add(lowered);
            }
        }
        return element;
    }

    /// <summary>The gap declaration: single value normally; "run main" pair when wrapping with a
    /// distinct RunGap (row-gap column-gap in the stacking order of the container's axis).</summary>
    private static string? GapValue(FlexNode flex, bool horizontal)
    {
        var run = flex.RunGap ?? flex.Gap;
        if (flex.Wrap && run != flex.Gap)
        {
            return horizontal
                ? $"{TokenCss.Px(run)} {TokenCss.Px(flex.Gap)}"
                : $"{TokenCss.Px(flex.Gap)} {TokenCss.Px(run)}";
        }
        return flex.Gap > 0 ? TokenCss.Px(flex.Gap) : null;
    }

    private static HtmlElement LowerText(Text text, ComponentContext context)
    {
        // The face can come from the NODE (this text is code) or from the STYLE (this ROLE is
        // code) — the native side merges the two into the style, and so does this.
        var mono = text.Mono || (text.StyleOverride?.Mono ?? context.Theme.Type(text.Role).Mono);
        // The slant reads the same two places for the same reason: a role may BE italic (a
        // theme's caption), and a node may slant a paragraph of an upright role.
        var italic = text.Italic || (text.StyleOverride?.Italic ?? context.Theme.Type(text.Role).Italic);
        // The OUTLINE, not the type scale: `h1`–`h6` when the author placed this text in the
        // document's structure, and a span when they did not. The heading's own UA margin and
        // size are cancelled in the token sheet (`.eq-type-*` owns the size), so choosing a level
        // moves nothing on screen — which is the point, since the level is a semantic decision and
        // a designer must not have to pay for it in layout.
        var element = new RealizedElement(text.HeadingLevel > 0 ? $"h{text.HeadingLevel}" : "span")
        {
            ClassName = $"eq-type-{text.Role.ToString().ToLowerInvariant()}",
            InnerHtml = text.Spans is null ? text.Content : null,
            Style = new HtmlStyle
            {
                Color = TokenCss.Value(text.Color ?? context.Theme.TextPrimary),
                // Line alignment inside the paragraph: wrapped lines of a centered headline must
                // center too — container alignment only places the block.
                TextAlign = text.Align switch
                {
                    TextAlignment.Center => Core.TextAlign.Center,
                    TextAlignment.End => Core.TextAlign.End,
                    _ => null,
                },
                // Authored \n is a HARD break (the designed headline's line turns) — pre-line
                // keeps normal wrapping between them.
                // Mono text is CODE (indentation survives); plain text only keeps its newlines.
                // PlainContent, not Content: a paragraph built from RUNS has an empty Content, so
                // reading the field instead of what the node says lost the break entirely — the
                // headline ran on in one line and nothing said why.
                WhiteSpace = mono ? "pre-wrap"
                    : text.PlainContent.Contains('\n') ? "pre-line" : null,
                FontFamily = mono ? TokenCss.MonoStack : null,
                FontVariantNumeric = text.Tabular ? "tabular-nums" : null,
                FontStyle = italic ? "italic" : null,
                // Spec S6: recolors glide (the design's transition-colors on nav labels/links).
                Transition = text.Transition is { } transition ? TokenCss.Transition(transition) : null,
            },
        };

        // RICH runs: each run flows inline with its own color/mono face; wrapping stays
        // paragraph-level because the children are inline spans.
        if (text.Spans is { } spans)
        {
            foreach (var run in spans)
            {
                // An <a> when the run links, a <span> otherwise. Both are inline, so the paragraph
                // still wraps between WORDS — which is the whole reason a link has to live in a run
                // rather than in a Row of Texts.
                var runElement = new RealizedElement(run.Destination is { Length: > 0 } ? "a" : "span")
                {
                    InnerHtml = run.Content,
                    Style = new HtmlStyle
                    {
                        Color = run.Color is { } runColor ? TokenCss.Value(runColor) : null,
                        FontFamily = run.Mono ? TokenCss.MonoStack : null,
                        FontWeight = run.Weight is { } runWeight ? ((int)runWeight).ToString() : null,
                        FontStyle = run.Italic ? "italic" : null,
                        // SIZE only — no line-height. A run keeps the paragraph's line box, which
                        // is what makes it a run rather than a line of its own.
                        FontSize = run.StyleOverride is { } runStyle
                            ? TokenCss.Px(runStyle.ScaledSize(context.TypeScale))
                            : null,
                    },
                };
                if (run.Destination is { Length: > 0 } destination)
                    runElement.RawAttributes = new Dictionary<string, string> { ["href"] = destination };
                element.Children.Add(runElement);
            }
        }

        // Gradient text: the background is painted through the glyphs. `color` stays as the
        // FALLBACK (a browser without background-clip:text renders readable solid text rather than
        // an invisible headline), and the fill-color override is what makes the clip visible.
        if (text.Gradient is { } gradient)
        {
            element.Style!.BackgroundImage = TokenCss.Gradient(gradient);
            element.Style.BackgroundClip = "text";
            element.Style.WebkitTextFillColor = "transparent";
        }

        // Single line → shaping-style ellipsis (spec A8).
        if (text.MaxLines == 1)
        {
            // MONO keeps `pre`: nowrap COLLAPSES runs of spaces, and in code the spaces ARE the
            // content — every indented line of the playground's editor started at column zero
            // while Photon (which draws literal glyph runs) indented it correctly. `pre` refuses
            // to wrap exactly like nowrap, so the ellipsis contract is untouched.
            element.Style!.WhiteSpace = mono ? "pre" : "nowrap";
            element.Style.Overflow = "hidden";
            element.Style.TextOverflow = "ellipsis";
            // BLOCK, or the other two do nothing. A Text lowers to a `span`, and `overflow` and
            // `text-overflow` are inert on a non-replaced INLINE box — so a squeezed single-line
            // Text painted its full width straight out of its parent instead of ellipsising inside
            // it: in a topbar that ran the placeholder over the ⌘K chip and off the screen. The
            // multi-line clamp below already sets a display for exactly this reason.
            //
            // Block rather than inline-block: block takes the width the parent allows, which is
            // what gives overflow something to clip against. Inline-block sizes to its content and
            // would spill again. Inside a flex row — where most single-line Texts live — the two
            // are identical, because a flex item is blockified either way.
            element.Style.Display = Core.Display.Block;
        }
        // MULTI-LINE clamp: the paragraph occupies exactly N lines and ends in an ellipsis — what
        // keeps a grid of cards on one baseline when the copy is not the site's to control (a
        // registry description, a user's bio). CSS line-clamp; Photon fence: the shaper truncates
        // at the line the box allows.
        else if (text.MaxLines > 1)
        {
            element.Style!.Display = Core.Display.WebkitBox;
            element.Style.WebkitBoxOrient = "vertical";
            element.Style.WebkitLineClamp = text.MaxLines.ToString();
            element.Style.Overflow = "hidden";
        }

        // System table override (e.g. Button labels) — inline styles beat the role class.
        if (text.StyleOverride is { } style)
        {
            element.Style!.FontSize = TokenCss.Px(style.Size);
            element.Style.LineHeight = TokenCss.Px(style.LineHeight);
            element.Style.FontWeight = ((int)style.Weight).ToString();
            element.Style.LetterSpacing = TokenCss.Px(style.Tracking);
        }

        return element;
    }

    /// <summary>Whether a node requests Fill on each axis — wrappers (Pressable's button) must
    /// stretch for the 100% chain to reach it (the native MeasureWrapper sizes to the child).</summary>
    /// <summary>
    /// The <c>background-image</c> layer LIST, in CSS paint order — the FIRST entry sits on top.
    /// The order (gradient → glow → grid) is the one the native realizer emits back-to-front, so a
    /// box carrying all three stacks identically on both targets.
    /// </summary>
    private static string? BackgroundLayers(BoxStyle style)
    {
        var layers = new List<string>(3);
        if (style.Gradient is { } gradient) layers.Add(TokenCss.Gradient(gradient));
        if (style.Glow is { } glow) layers.Add(TokenCss.Glow(glow));
        if (style.Pattern is { } pattern) layers.Add(TokenCss.GridPattern(pattern));
        return layers.Count == 0 ? null : string.Join(", ", layers);
    }

    /// <summary>The matching <c>background-size</c> list: one entry per layer (the grid contributes
    /// TWO), with `auto` standing in for the layers that size themselves.</summary>
    private static string? BackgroundLayerSizes(BoxStyle style)
    {
        if (style.Pattern is not { } pattern) return null;
        var sizes = new List<string>(3);
        if (style.Gradient is not null) sizes.Add("auto");
        if (style.Glow is not null) sizes.Add("auto");
        sizes.Add(TokenCss.GridPatternSize(pattern));
        return string.Join(", ", sizes);
    }

    /// <summary>
    /// The cap a FILL child puts on itself, walked the same way <see cref="Fills"/> walks the fill
    /// — 0 when there is none.
    /// <para>
    /// A wrapper stands in for its child in the parent's layout, so it has to carry the whole
    /// width contract and not half of it. Taking the 100% and dropping the max-width made the
    /// wrapper full width with a narrower block inside: the child hugged the start edge, and a row
    /// centring its children had an item already filling the row, which is nothing to centre.
    /// Measured on a real page before it was believed — a card capped at 980 inside a 1392 wrapper.
    /// </para>
    /// </summary>
    private static SizeValue CapsAt(VisualNode node) => node switch
    {
        Box box => box.Style.MaxWidth,
        Pressable pressable => CapsAt(pressable.Child),
        Hoverable hoverable => CapsAt(hoverable.Child),
        Adjustable adjustable => CapsAt(adjustable.Child),
        Flexible flexible => CapsAt(flexible.Child),
        LoopMotion motion => CapsAt(motion.Child),
        Link link => CapsAt(link.Child),
        _ => SizeValue.Hug,
    };

    private static (bool Width, bool Height) Fills(VisualNode node) => node switch
    {
        Box box => (box.Style.Width.Kind == SizeKind.Fill, box.Style.Height.Kind == SizeKind.Fill),
        FlexNode flex => (flex.Width.Kind == SizeKind.Fill, flex.Height.Kind == SizeKind.Fill),
        Stack stack => (stack.Width.Kind == SizeKind.Fill, stack.Height.Kind == SizeKind.Fill),
        Pressable pressable => Fills(pressable.Child),
        Hoverable hoverable => Fills(hoverable.Child),
        Adjustable adjustable => Fills(adjustable.Child),
        Flexible flexible => Fills(flexible.Child),
        LoopMotion motion => Fills(motion.Child),
        _ => (false, false),
    };

    /// <summary>
    /// Whether a lowered subtree already carries an interactive ELEMENT, in which case a Pressable
    /// around it must not become a second one. HTML forbids a button inside a button (and an anchor
    /// inside an anchor): the parser closes the outer one and hands back an empty shell, so the
    /// wrapper renders as nothing and the hydrated tree disagrees with the served HTML about the
    /// whole subtree.
    /// <para>
    /// FORM CONTROLS count for the same reason, and it is not a style preference: the content model
    /// forbids interactive content inside a <c>button</c>, and a browser handed
    /// <c>button &gt; input</c> resolves it by taking the typing away.
    /// </para>
    /// </summary>
    private static bool WrapsAnInteractive(IComponent node) =>
        node is RealizedElement element
        && (element.Tag is "button" or "a" or "input" or "select" or "textarea"
            || element.Children.Any(WrapsAnInteractive));

    private static HtmlElement LowerPressable(Pressable pressable, ComponentContext context)
    {
        var fills = Fills(pressable.Child);
        var cap = CapsAt(pressable.Child);
        var child = LowerNode(pressable.Child, context, horizontalAxis: null);

        // A Pressable AROUND a control — a Menu making its trigger open the panel — is ordinary
        // composition, and the trigger is usually a Button. The OUTER one yields: it keeps the
        // press, the child keeps being the real control, and the markup stays legal.
        var wrapping = child is not null && WrapsAnInteractive(child);
        var element = new RealizedElement(wrapping ? "span" : "button")
        {
            // Neutralize UA button chrome — the child carries ALL visuals (same as native).
            Style = new HtmlStyle
            {
                Padding = "0",
                Border = "none",
                Background = "none",
                FontFamily = "inherit",
                Cursor = pressable.Disabled ? null : "pointer",
                TextAlign = TextAlign.Start,
                // A Fill child needs the 100% chain to pass through the button (scrim et al.).
                Width = fills.Width ? "100%" : null,
                MaxWidth = Size(cap),
                // A control is a target by definition, and says so because `none` inherits from
                // any transparent row above it.
                PointerEvents = "auto",
                Height = fills.Height ? "100%" : null,
            },
            Disabled = pressable.Disabled && !wrapping ? true : null,
            AriaLabel = pressable.Label,
            // Selection stated, not merely painted — a fill colour says nothing to a screen reader.
            AriaPressed = pressable.Selected is { } selected ? (selected ? "true" : "false") : null,
            // Disclosure stated the same way: a rotated chevron is paint, aria-expanded is the answer.
            AriaExpanded = pressable.Expanded,
            OnClick = pressable.Disabled ? null : pressable.OnPressed,
        };

        // Interaction states (spec §01): mechanics live in the GENERATED stylesheet — every enabled
        // pressable carries the class (the :focus-visible double ring is an accessibility DEFAULT);
        // the pressed swap additionally needs its token value as a per-element custom property.
        if (!pressable.Disabled)
        {
            // eq-pressed carries the same declaration :active does — see TokenCss. It is added, not
            // substituted, so a simulated control still behaves like the control it is picturing.
            element.ClassName = _simulated.Value.HasFlag(SimulatedState.Pressed)
                ? "eq-pressable eq-pressed"
                : "eq-pressable";
            if (pressable.PressedBackground is { } pressedFill)
            {
                element.Style!.CustomProperties = new Dictionary<string, string>
                {
                    ["--eq-pressed-bg"] = TokenCss.Value(pressedFill),
                };
            }
        }

        // §10 initial focus: the marker the trap controller prefers over the first focusable —
        // static, identical on both producers, so hydration compares equal.
        if (pressable.InitialFocus)
        {
            element.RawAttributes ??= new Dictionary<string, string>();
            element.RawAttributes["data-eq-initial-focus"] = "";
        }

        // A span that acts as a button says so, and takes the keyboard the same way.
        if (wrapping)
        {
            element.Role = "button";
            if (pressable.Disabled) element.AriaDisabled = true;
            else element.TabIndex = 0;
        }

        // One choice of an exclusive set (role: radio): the wrapping Adjustable is the one Tab
        // stop, so the item leaves the tab order (roving) and states its selection as
        // checked-ness, not pressed-ness. TS twin emits the same three attributes.
        if (pressable.Role == PressableRole.Radio)
        {
            element.Role = "radio";
            element.AriaChecked = pressable.Selected == true ? "true" : "false";
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // One tab of a tablist — the radio's twin (roving, the wrapping Adjustable is the one Tab
        // stop), except a tab is PICKED, not checked: aria-selected is its word.
        if (pressable.Role == PressableRole.Tab)
        {
            element.Role = "tab";
            element.AriaSelected = pressable.Selected == true;
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // Panel item rows: out of the Tab order — the keyboard lives on the TRIGGER while the
        // panel is up, and the highlight travels as aria-activedescendant (see LowerAnchored).
        if (pressable.Role == PressableRole.MenuItem)
        {
            element.Role = "menuitem";
            element.AriaPressed = null;
            element.TabIndex = -1;
        }
        if (pressable.Role == PressableRole.Option)
        {
            element.Role = "option";
            element.AriaSelected = pressable.Selected == true;
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // One cell of a 2-D composite: PICKED like a tab or an option (design system C15 asks for
        // gridcell + selected), and out of the Tab order because the enclosing Navigable is the
        // composite's one stop — 42 days must not be 42 tab presses.
        if (pressable.Role == PressableRole.GridCell)
        {
            element.Role = "gridcell";
            element.AriaSelected = pressable.Selected == true;
            element.AriaPressed = null;
            element.TabIndex = -1;
        }

        // A navigation destination keeps its Tab stop (a nav is a list of links, not a composite)
        // and says WHERE YOU ARE, which is the one thing aria-pressed/selected/checked cannot say.
        // Only the current one carries the attribute: aria-current="false" on the others is legal
        // and pure noise, and every destination announcing "not current" is worse than silence.
        if (pressable.Role == PressableRole.Destination)
        {
            element.AriaPressed = null;
            if (pressable.Selected == true) element.AriaCurrent = "page";
        }

        // A check states its state as an ATTRIBUTE, never in the name — a name that changes when
        // the state does reads as a different control. Unlike a radio it keeps its own Tab stop
        // (the button element already is one): a checkbox is not one choice of a composite.
        if (pressable.Role is PressableRole.Checkbox or PressableRole.Switch)
        {
            element.Role = pressable.Role == PressableRole.Switch ? "switch" : "checkbox";
            // "mixed" exists for checkboxes and nothing else — ARIA's own rule, enforced here so a
            // Switch cannot half-say something the role has no word for.
            element.AriaChecked =
                pressable.Mixed && pressable.Role == PressableRole.Checkbox ? "mixed"
                : pressable.Selected == true ? "true" : "false";
            element.AriaPressed = null;
        }

        if (child is not null) element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Pointer-presence callback (S5 programmable): a layout-transparent div carrying
    /// mouseenter/mouseleave — the events the low-level element pipeline already hydrates. The
    /// child owns all visuals; display:contents would break the events, so the div participates
    /// as a plain wrapper passing Fill through like Pressable's button does.
    /// </summary>
    private static HtmlElement LowerHoverable(Hoverable hoverable, ComponentContext context)
    {
        var fills = Fills(hoverable.Child);
        var cap = CapsAt(hoverable.Child);
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : null,
                MaxWidth = Size(cap),
                // A control is a target by definition, and says so because `none` inherits from
                // any transparent row above it.
                PointerEvents = "auto",
                Height = fills.Height ? "100%" : null,
            },
            OnMouseEnter = _ => hoverable.OnChanged(true),
            OnMouseLeave = _ => hoverable.OnChanged(false),
        };
        if (LowerNode(hoverable.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Navigation semantics: a REAL <c>&lt;a destination&gt;</c> (SSR-crawlable, router-intercepted) whose
    /// UA chrome is neutralized — the child owns all visuals, exactly the Pressable contract. A Fill
    /// child gets the 100% pass-through chain the same way.
    /// </summary>
    private static HtmlElement LowerLink(Link link, ComponentContext context)
    {
        var fills = Fills(link.Child);
        var cap = CapsAt(link.Child);
        // The render's link policy — the language prefix, when the app asked for one. Applied HERE
        // so an author writes `/pricing` once and the href is right in every language.
        var destination = RenderContext.ResolveDestination(link.Destination);
        var element = new RealizedElement("a")
        {
            ClassName = "eq-link",
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : null,
                MaxWidth = Size(cap),
                // A control is a target by definition, and says so because `none` inherits from
                // any transparent row above it.
                PointerEvents = "auto",
                Height = fills.Height ? "100%" : null,
            },
            AriaLabel = link.Label,
            RawAttributes = new Dictionary<string, string> { ["href"] = destination },
        };
        // Read by the router when it decides where the new page starts. On the anchor rather than in
        // a side table because the router meets the ANCHOR — a delegated click listener, one for the
        // whole document, which is also what makes server-rendered links work with no wiring.
        if (link.KeepsPosition) element.RawAttributes["data-eq-keep-position"] = "";
        // WARM ON HOVER. The router has always had a prefetch seam and nothing ever marked a link
        // for it, so it was dead code and every navigation paid the module import in full — which is
        // most of what a click costs. The router asks once per link and swallows failures, so the
        // worst case is the work the click was going to do anyway, done slightly earlier.
        //
        // App-internal destinations only: an absolute URL belongs to somebody else's server.
        if (destination.StartsWith('/')) element.RawAttributes["data-prefetch"] = "";
        // The page you are ON. Only the current link carries it — aria-current="false" on the other
        // nine is legal, useless, and read out loud.
        if (link.Current) element.AriaCurrent = "page";
        if (LowerNode(link.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>
    /// Enter motion (spec §06): NO wrapper element — the mount-playing animation class rides the
    /// lowered child's own root, so flex/stack layout is untouched and the reconciler's in-place
    /// patching never restarts the animation on unrelated re-renders (it replays only on a real
    /// re-insertion — exactly the declarative-presence contract).
    /// </summary>
    private static HtmlElement? LowerPresence(Presence presence, ComponentContext context, bool? horizontalAxis)
    {
        if (LowerNode(presence.Child, context, horizontalAxis) is not { } child) return null;
        var slide = presence.Enter == PresenceMotion.SlideUp;
        var cls = slide ? "eq-presence-slideup" : "eq-presence-fade";
        child.ClassName = string.IsNullOrEmpty(child.ClassName) ? cls : $"{cls} {child.ClassName}";
        // The EXIT marker: the reconciler defers this element's removal (reparented to body inside
        // its fixed overlay layer) while the reverse animation plays.
        if (child is RealizedElement realized)
        {
            realized.RawAttributes ??= new Dictionary<string, string>();
            realized.RawAttributes["data-eq-exit"] = slide ? "slideup" : "fade";
        }
        return child;
    }

    /// <summary>
    /// Gestures v2 (SSR half): the drag marker rides the child's own root — the CLIENT runtime's
    /// pointer-capture controller drives the actual drag (the server only emits the marker; the
    /// dismiss callback attaches client-side through the lowering mirror's custom event).
    /// </summary>
    /// <summary>
    /// A continuous gesture: the child carries its RULES as data, and one document-level controller
    /// in the runtime does the tracking. The rest offset is a plain transform, so a row that is
    /// already open renders open on the server too, before any script has run.
    /// </summary>
    private static HtmlElement? LowerDraggable(Draggable draggable, ComponentContext context,
        bool? horizontalAxis)
    {
        if (LowerNode(draggable.Child, context, horizontalAxis) is not RealizedElement child) return null;

        // A drag surface takes the pointer, so the node that carries the gesture declares itself:
        // `none` inherits from any transparent row above it, and a surface that is not a target
        // never receives the pointerdown the drag starts from.
        child.Style ??= new HtmlStyle();
        child.Style.PointerEvents = "auto";

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        child.RawAttributes ??= new Dictionary<string, string>();
        child.RawAttributes["data-eq-drag"] = draggable.Axis == DragAxis.Horizontal ? "x" : "y";
        child.RawAttributes["data-eq-drag-min"] = draggable.Min.ToString(culture);
        child.RawAttributes["data-eq-drag-max"] = draggable.Max.ToString(culture);
        child.RawAttributes["data-eq-drag-rest"] = draggable.RestOffset.ToString(culture);
        if (draggable.Normalized) child.RawAttributes["data-eq-drag-normalized"] = "1";
        if (!draggable.Follows) child.RawAttributes["data-eq-drag-follows"] = "0";
        if (draggable.OnMoved is not null) child.RawAttributes["data-eq-drag-moves"] = "1";

        // A gesture the caller paints itself is already where it belongs — translating the rest as
        // well would move it twice.
        if (draggable.RestOffset != 0 && draggable.Follows)
        {
            child.Style ??= new HtmlStyle();
            child.Style.Transform = draggable.Axis == DragAxis.Horizontal
                ? $"translateX({TokenCss.Px(draggable.RestOffset)})"
                : $"translateY({TokenCss.Px(draggable.RestOffset)})";
            child.Style.Transition = $"transform {Motion.BaseMs}ms";
        }

        // The release callback rides the SSR bridge the same way every other handler does; the
        // client half reads the offset off the event the controller dispatches.
        if (draggable.OnReleased is { } released)
        {
            child.CustomEvents["eq-drag-released"] = released;
        }

        if (draggable.OnMoved is { } moved)
        {
            child.CustomEvents["eq-drag-moved"] = moved;
        }

        return child;
    }

    private static HtmlElement? LowerDragDismiss(DragDismiss drag, ComponentContext context, bool? horizontalAxis)
    {
        if (LowerNode(drag.Child, context, horizontalAxis) is not { } child) return null;
        if (child is RealizedElement target)
        {
            // Same rule as Draggable: the surface the gesture starts on declares itself a target,
            // because `none` inherits from any transparent row above it.
            target.Style ??= new HtmlStyle();
            target.Style.PointerEvents = "auto";
        }
        if (child is RealizedElement realized)
        {
            realized.RawAttributes ??= new Dictionary<string, string>();
            realized.RawAttributes["data-eq-drag-dismiss"] =
                DragDismiss.ThresholdDp.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return child;
    }

    private static HtmlElement LowerFlexible(Flexible flexible, ComponentContext context, bool? horizontalAxis)
    {
        // flex: n s basis. A basis of 0 (the default) matches the native engine's leftover-by-weight
        // distribution, and min-size 0 lets text children shrink to ellipsis instead of pushing
        // siblings (the truncation contract).
        //
        // A POSITIVE basis is the size the line breaker measures against, which is the only way a
        // wrapping row can decide to break rather than squeeze — so it has to reach the CSS. Both it
        // and Shrink were hardcoded here (`{Flex} 1 0%`) while the TS twin emitted them, so SSR and
        // hydration disagreed on the class, and on the first paint a wrapping row measured ZERO for
        // a child asking for 220: it never broke the line and shrank the child to nothing instead.
        var basis = flexible.Basis > 0 ? TokenCss.Px(flexible.Basis) : "0%";
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // Spec B14 value transitions: weight changes animate Base/standard; the component
                // omits the flag on a regression so the change SNAPS (forward-only honesty).
                Transition = flexible.AnimateChanges
                    ? "flex-grow var(--eq-motion-base) var(--eq-curve-standard)"
                    : null,
                Flex = $"{flexible.Flex} {flexible.Shrink} {basis}",
                MinWidth = horizontalAxis is not false ? "0" : null,
                MinHeight = horizontalAxis is false ? "0" : null,
            },
        };
        if (LowerNode(flexible.Child, context, horizontalAxis) is { } child)
            element.Children.Add(child);
        return element;
    }

    private static HtmlElement? LowerSpacer(Spacer spacer, bool? horizontalAxis)
    {
        if (horizontalAxis is null) return null; // layout-only outside a flex container

        // Spec B14: a weighted spacer is the ratio's COUNTERWEIGHT (ProgressBar), so its weight changes
        // must animate exactly like the Flexible fill's — otherwise the fill glides while the spacer
        // snaps, and SSR loses class identity against the TS twin (lowerSpacer).
        var style = spacer.Flex > 0
            ? new HtmlStyle
            {
                Transition = spacer.AnimateChanges
                    ? "flex-grow var(--eq-motion-base) var(--eq-curve-standard)"
                    : null,
                Flex = $"{spacer.Flex} 1 0%",
            }
            : horizontalAxis.Value
                ? new HtmlStyle { Width = TokenCss.Px(spacer.FixedLength), FlexShrink = "0" }
                : new HtmlStyle { Height = TokenCss.Px(spacer.FixedLength), FlexShrink = "0" };
        return new RealizedElement("div") { Style = style, AriaHidden = true };
    }

    /// <summary>
    /// A FIXED size does not shrink. CSS disagrees by default — a flex item's `flex-shrink` is 1,
    /// so a box with `width: 68px` next to an overflowing sibling is quietly squeezed, and
    /// everything inside it moves. Photon never does that: fixed is fixed, on both targets.
    /// (The code editor's gutter was the symptom — line numbers slid left on exactly the lines whose
    /// code ran past the viewport, and slid back when a scroll took those lines out of the window.)
    /// </summary>
    private static string? Rigid(SizeValue width, SizeValue height) =>
        width.Kind == SizeKind.Fixed || height.Kind == SizeKind.Fixed ? "0" : null;

    /// <summary>
    /// A size on ONE axis. The axis matters for exactly one kind: the window is `100vh` down and
    /// `100vw` across, and every other kind reads the same either way.
    /// </summary>
    private static string? Size(SizeValue value, bool vertical = false) => value.Kind switch
    {
        SizeKind.Fixed => TokenCss.Px(value.Value),
        SizeKind.Fill => "100%",
        // The window, less an inset. `calc` rather than a bare `vh` because the inset is the whole
        // point — see SizeKind.WindowMinus for why a constant cannot express this.
        SizeKind.WindowMinus => value.Value > 0
            ? $"calc(100{(vertical ? "vh" : "vw")} - {TokenCss.Px(value.Value)})"
            : $"100{(vertical ? "vh" : "vw")}",
        _ => null, // Hug = auto
    };
}

/// <summary>A lowered element: a generic <see cref="HtmlElement"/> with an explicit tag — the web
/// realizer's only output shape (mirrors the web SDK's generic div container).</summary>
internal sealed class RealizedElement : HtmlElement, IPseudoStyled, IAdaptiveGated, IScrolledStyled
{
    /// <summary>Spec S6: the size-class gate this element carries (fixed class + media blob).</summary>
    public string? AdaptiveGate { get; set; }

    public RealizedElement(string tag) => Tag = tag;

    public string Tag { get; }

    /// <summary>Spec S5: hover/focus diff declarations, converted to pseudo-variant atomic rules by
    /// the atomizer pass (pseudo-classes need the ATOMIC pipeline — inline styles can't express them).</summary>
    public List<(string Pseudo, string Prop, string Value)> PseudoDeclarations { get; } = new();

    /// <summary>Sticky.ScrolledStyle: declarations gated by the root's <c>eq-scrolled</c> class
    /// (the runtime scroll listener) — converted by the atomizer like pseudo variants.</summary>
    public List<(string Prop, string Value)> ScrolledDeclarations { get; } = new();

    /// <summary>Attributes emitted VERBATIM (no data- prefix) — SVG needs viewBox/fill/d as-is.</summary>
    public Dictionary<string, string>? RawAttributes { get; set; }

    public override HtmlNode Render()
    {
        var children = Children.Select(c => c.Render()).ToList();
        if (!string.IsNullOrEmpty(InnerHtml))
            children.Insert(0, HtmlNode.Text(InnerHtml));

        var attributes = BuildAttributes();
        if (RawAttributes != null)
        {
            foreach (var raw in RawAttributes) attributes[raw.Key] = raw.Value;
        }

        return new HtmlNode
        {
            Tag = Tag,
            Attributes = attributes,
            Events = BuildEvents(),
            Children = children,
        };
    }
}
