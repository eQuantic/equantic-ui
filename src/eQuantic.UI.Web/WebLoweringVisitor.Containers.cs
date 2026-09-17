using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Containers and layout — the fourteen words that place other words. Their helpers live here with
/// them: the flex mapping Row and Column share, the anchor panel, the pinned layers, and the
/// box-style machinery the audit measured as the bulk of this realizer.
/// </summary>
internal sealed partial class WebLoweringVisitor
{
    /// <summary>
    /// A Stack child as the STACK sees it: a component is expanded to what it builds, so a
    /// `Positioned` returned by one still positions. Only for the positioning decision — the node
    /// that comes back is lowered normally.
    /// </summary>
    private VisualNode ResolveForPositioning(VisualNode child)
    {
        // Bounded: a component whose build returns itself would otherwise spin here.
        for (var hops = 0; hops < 8 && child is UiComponent component; hops++)
        {
            child = component.BuildContained(_context);
        }
        return child;
    }

    /// <summary>
    /// Spec A3 lowering: single-cell CSS grid — every NON-positioned child sits in cell 1/1
    /// (overlapping, painted in child order) with <c>place-items</c> carrying the alignment;
    /// Positioned children wrap in <c>position:absolute</c> against the stack's
    /// <c>position:relative</c> frame, with signed offsets (End→right, Start→left).
    /// </summary>
    private HtmlElement LowerStack(Stack stack)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Display = Display.Grid,
                Position = Position.Relative,
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
            var child = ResolveForPositioning(raw);
            if (child is Positioned positioned)
            {
                var lowered = Lower(positioned.Child, horizontalAxis: null);
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
                        Position = Position.Absolute,
                        Top = positioned.Top is { } top ? TokenCss.Px(top) : spanY ? "0" : null,
                        Right = positioned.End is { } end ? TokenCss.Px(end) : spanX ? "0" : null,
                        Bottom = positioned.Bottom is { } bottom ? TokenCss.Px(bottom) : spanY ? "0" : null,
                        Left = positioned.Start is { } start ? TokenCss.Px(start) : spanX ? "0" : null,
                        // Spec S7: explicit stacking WINS; otherwise the child's own depth.
                        ZIndex = (positioned.Layer != 0 ? positioned.Layer : depth).ToString(),
                    },
                };
                anchor.Children.Add(lowered);
                element.Children.Add(anchor);
            }
            else
            {
                var lowered = Lower(child, horizontalAxis: null);
                if (lowered is null) continue;
                // The cell IS the stack's available space (the native MeasureStack contract): it
                // stretches to the single grid cell and aligns its child via flex — so a Fill child
                // covers the stack while a hug child sits at the Stack.Align anchor.
                var cell = new RealizedElement("div")
                {
                    Style = new HtmlStyle
                    {
                        GridArea = "1 / 1",
                        Display = Display.Flex,
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

    /// <summary>
    /// Wave 3 anchored overlay: a position:relative host wrapping the anchor; while Open, an
    /// invisible fixed scrim (a real Pressable — tap-outside dismisses through the ordinary event
    /// pipeline) and the absolute panel, positioned ENTIRELY by the generated placement classes
    /// (no JS). The gap rides the margin on the placement axis as an atomic declaration. Keep
    /// anchors out of overflow-hidden scrollers and transformed subtrees (the documented fences).
    /// </summary>
    private HtmlElement LowerAnchored(Anchored anchored)
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

        if (Lower(anchored.Anchor, horizontalAxis: null) is { } anchor)
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
            var hoverPanel = BuildAnchorPanel(anchored);
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
            && Lower(new Pressable(scrimBox, dismiss) { Label = "Dismiss" }, horizontalAxis: null) is { } scrim)
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

        var openPanel = BuildAnchorPanel(anchored);
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
    private void NumberItemRows(HtmlElement element, string panelId, ref int next)
    {
        if (element.Role is "menuitem" or "option")
        {
            element.Id = $"{panelId}-{next}";
            next++;
        }
        foreach (var child in element.Children)
            if (child is HtmlElement childElement)
                NumberItemRows(childElement, panelId, ref next);
    }

    /// <summary>The panel's visible text, flattened — what the tooltip id hashes. Walking the
    /// VISUAL tree (not the lowered one) keeps the answer identical on both producers.
    /// <para>
    /// PlainContent, not Content: a paragraph built from RUNS has an empty Content, so every
    /// styled panel hashed the same empty string and shared one id with all the others — and the
    /// aria-describedby pointing at it then named somebody else's panel.
    /// </para></summary>
    private string TextContentOf(VisualNode node) => node switch
    {
        Text text => text.PlainContent,
        Box box => box.Child is { } child ? TextContentOf(child) : "",
        FlexNode flex => string.Concat(flex.Children.Select(TextContentOf)),
        Pressable pressable => TextContentOf(pressable.Child),
        _ => "",
    };

    private RealizedElement BuildAnchorPanel(Anchored anchored)
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
        if (Lower(anchored.Panel, horizontalAxis: null) is { } content)
            panel.Children.Add(content);
        return panel;
    }

    /// <summary>
    /// The Anchored open/close transition list: opacity (+ transform for the panel) riding the
    /// spec's duration/easing, and ALWAYS visibility — it flips at the fade's end on exit and its
    /// start on enter (the CSS discrete-interpolation rule), which is what parks a closed panel
    /// outside hit-testing and the focus order without JS.
    /// </summary>
    private string MotionTransition(TransitionSpec motion, bool withTransform)
    {
        var timing = $"{TokenCss.Number(motion.DurationMs)}ms {TokenCss.Bezier(motion.Easing)}";
        return withTransform
            ? $"opacity {timing}, transform {timing}, visibility {timing}"
            : $"opacity {timing}, visibility {timing}";
    }

    private string PlacementClass(AnchorPlacement placement) => placement switch
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
    private HtmlElement LowerSafeArea(SafeArea safeArea)
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

        if (Lower(safeArea.Child, horizontalAxis: null) is { } child)
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

    /// <summary>Spec S7: scroll-anchored chrome — in flow until scrolling would push it out, then
    /// pinned at <c>Offset</c> from the viewport start (CSS <c>position: sticky</c>; v1 vertical).</summary>
    private HtmlElement LowerPinned(Pinned pinned)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // FLOATING chrome (the fixed header) paints above the page and takes no layout
                // space; plain pinned stays in flow and pins when scrolling reaches it.
                Position = pinned.Float ? Position.Fixed : Position.Sticky,
                Top = TokenCss.Px(pinned.Offset),
                Left = pinned.Float ? "0" : null,
                Right = pinned.Float ? "0" : null,
                // CHROME, above anything the CONTENT can reach. Plain pinned used to sit at 1, one
                // step above nothing, which is a number competing with other numbers: a raised card
                // (elevation carries 1–5 now) or a deep enough stack cell would out-stack the pinned
                // header and scroll straight over it. Chrome is not "slightly raised content", it is
                // a different plane, and saying so here is what keeps the author out of the argument.
                // FLOATING chrome is a band above PINNED chrome, so a header and a rail on one page
                // do not tie and let document order pick the winner.
                ZIndex = pinned.Float ? FloatingChromeLayer : PinnedLayer,
                // Spec S6: the scrolled swap GLIDES (the design's transparent-until-scrolled bar
                // fades its veil in) instead of flipping in one frame.
                Transition = pinned.Transition is { } transition ? TokenCss.Transition(transition) : null,
            },
        };

        // SCROLL-LINKED diff: each declaration lands under the root-gated scrolled variant; the
        // runtime's scroll listener toggles `eq-scrolled` on <html>.
        if (pinned.ScrolledStyle is { IsEmpty: false } scrolled)
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

        if (Lower(pinned.Child, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        // Marked so the runtime can MEASURE it: a bookmark keeps room above itself equal to the
        // chrome that would cover it, and the height of a content-sized bar is not knowable here.
        // Emitted by SSR too, or the hydrated DOM would differ from this one by an attribute.
        (element.DataAttributes ??= new Dictionary<string, string>())["eq-pinned"] = "1";

        return element;
    }

    /// <summary>Spec A6 lowering: native browser scrolling — <c>overflow-y/x: auto</c> on the axis
    /// (the browser owns physics, momentum and the scrollbar); the cross axis stays hidden so content
    /// never leaks. The programmatic Offset is a native-side concept (browser scroll state lives in
    /// the DOM).</summary>
    private HtmlElement LowerScrollView(ScrollView scroll)
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
        var child = Lower(scroll.Child, horizontalAxis: null);
        if (child != null) element.Children.Add(child);
        return element;
    }

    // The two halves of CSS `place-items` = "<align> <justify>" (vertical then horizontal).
    /// <summary>Horizontal anchor of the 9-point alignment → flex justify-content.</summary>
    private JustifyContent AlignmentJustify(Alignment align) => ((int)align % 3) switch
    {
        1 => JustifyContent.Center,
        2 => JustifyContent.FlexEnd,
        _ => JustifyContent.FlexStart,
    };

    /// <summary>Vertical anchor of the 9-point alignment → flex align-items.</summary>
    private AlignItem AlignmentAlign(Alignment align) => ((int)align / 3) switch
    {
        1 => AlignItem.Center,
        2 => AlignItem.FlexEnd,
        _ => AlignItem.FlexStart,
    };
    /// <summary>
    /// Phase C viewport layer: a generated fixed inset-0 stacking layer (.eq-overlay) — the child
    /// owns its composition (scrim, centering) from the ordinary vocabulary. Fixed positioning
    /// escapes the page flow visually without a portal; keep Overlays out of transformed subtrees
    /// (LoopMotion) — CSS transforms re-anchor fixed descendants.
    /// </summary>
    private HtmlElement LowerOverlay(Overlay overlay)
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
        if (Lower(overlay.Child, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
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
    private bool PaintsNothing(Box box) =>
        box.Child is null
        && box.Style.Background is null
        && box.Style.Gradient is null
        && box.Style.Glow is null
        && box.Style.Pattern is null
        && box.Style.BorderWidth == 0
        && box.Style.Elevation == 0;

    private HtmlElement LowerBox(Box box)
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
                    style.Elevation > 0 && !_context.Theme.Elevation(style.Elevation).IsNone
                        ? TokenCss.Shadow(_context.Theme.Elevation(style.Elevation))
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
        // the chrome into a layer of its own (which is what Overlay is, and a pinned header is not).
        // Elevation is the word the design system already has for "above".
        if (style.Elevation > 0)
        {
            element.Style!.ZIndex = style.Elevation.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // z-index needs a positioned box, and `relative` is the one that changes nothing else.
            // A box that already positions itself — pinned chrome, an absolute anchor — keeps its own.
            element.Style.Position ??= Position.Relative;
        }

        // A SIMULATED state REPLACES the base declarations; otherwise the diff rides the
        // pseudo-class it belongs to. Replacing rather than adding a second class of the same
        // property matters: atomic classes all have equal specificity, so two of them on one
        // element are decided by stylesheet insertion order — which depends on what the rest of
        // the page happened to emit first.
        var simulated = _simulated;
        if (box.Style.Hover is { IsEmpty: false } hover)
        {
            if (simulated.HasFlag(SimulatedState.Hovered)) ApplyDiff(element.Style, hover);
            else AppendDiff(element, ":hover", hover);
        }
        if (box.Style.Focus is { IsEmpty: false } focus)
        {
            if (simulated.HasFlag(SimulatedState.Focused)) ApplyDiff(element.Style, focus);
            else AppendDiff(element, ":focus-visible", focus);
        }

        if (box.Child is not null && Lower(box.Child, horizontalAxis: null) is { } child)
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
    private bool StretchesChildHeight(BoxStyle style, VisualNode child)
    {
        if (style.Height.Kind == SizeKind.Hug) return false;
        return child switch
        {
            Box inner => inner.Style.Height.Kind == SizeKind.Hug,
            FlexNode flex => flex.Height.Kind == SizeKind.Hug,
            _ => false,
        };
    }

    /// <summary>
    /// <c>border-width</c> in CSS's own order — top, right, bottom, left — from the logical sides.
    /// Start/End map to left/right here because this is the LTR realization; a right-to-left
    /// document flips them in the same place every other logical property is flipped.
    /// </summary>
    private string SideWidths(float width, BorderSides sides)
    {
        var w = TokenCss.Px(width);
        string Edge(BorderSides side) => sides.HasFlag(side) ? w : "0";
        return $"{Edge(BorderSides.Top)} {Edge(BorderSides.End)} {Edge(BorderSides.Bottom)} {Edge(BorderSides.Start)}";
    }

    /// <summary>One box-shadow list from the optional parts (null when none are set).</summary>
    private string? ComposeShadows(params string?[] parts)
    {
        var present = parts.Where(part => part != null).ToList();
        return present.Count == 0 ? null : string.Join(", ", present);
    }

    /// <summary>
    /// The same members, written over the BASE style — what a simulated state does. The pairs here
    /// mirror <see cref="AppendDiff"/> exactly, so a preview shows the declarations a real hover
    /// would produce rather than an approximation of them.
    /// </summary>
    private void ApplyDiff(HtmlStyle? style, in StyleDiff diff)
    {
        if (style is null) return;
        if (diff.Background is { } bg) style.BackgroundColor = TokenCss.Value(bg);
        if (diff is { BorderWidth: { } bw, BorderColor: { } bc })
            style.Border = $"{TokenCss.Px(bw)} solid {TokenCss.Value(bc)}";
        else if (diff.BorderColor is { } onlyColor) style.BorderColor = TokenCss.Value(onlyColor);
        if (diff.Elevation is { } level && !_context.Theme.Elevation(level).IsNone)
            style.BoxShadow = TokenCss.Shadow(_context.Theme.Elevation(level));
        if (diff.Opacity is { } alpha) style.Opacity = TokenCss.Number(alpha);
        if (diff.Gradient is { } gradient) style.BackgroundImage = TokenCss.Gradient(gradient);
    }

    /// <summary>Spec S5: a StyleDiff's set members as pseudo-state declarations (base values keep).</summary>
    private void AppendDiff(RealizedElement element, string pseudo, in StyleDiff diff)
    {
        if (diff.Background is { } bg)
            element.PseudoDeclarations.Add((pseudo, "background-color", TokenCss.Value(bg)));
        if (diff is { BorderWidth: { } bw, BorderColor: { } bc })
            element.PseudoDeclarations.Add((pseudo, "border", $"{TokenCss.Px(bw)} solid {TokenCss.Value(bc)}"));
        else if (diff.BorderColor is { } onlyColor)
            element.PseudoDeclarations.Add((pseudo, "border-color", TokenCss.Value(onlyColor)));
        if (diff.Elevation is { } level && !_context.Theme.Elevation(level).IsNone)
            element.PseudoDeclarations.Add((pseudo, "box-shadow", TokenCss.Shadow(_context.Theme.Elevation(level))));
        if (diff.Opacity is { } alpha)
            element.PseudoDeclarations.Add((pseudo, "opacity", TokenCss.Number(alpha)));
        if (diff.Gradient is { } gradient)
            element.PseudoDeclarations.Add((pseudo, "background-image", TokenCss.Gradient(gradient)));
    }
    private HtmlElement LowerFlex(FlexNode flex)
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
            if (Lower(child, horizontal) is { } lowered)
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
    private HtmlElement LowerAdaptive(AdaptiveNode adaptive)
    {
        var wrapper = new RealizedElement("div") { Style = new HtmlStyle { Display = Display.Contents } };

        void AddVariant(VisualNode variant, string gate)
        {
            if (Lower(variant, horizontalAxis: null) is not { } lowered) return;
            var gated = new RealizedElement("div") { AdaptiveGate = gate };
            gated.Children.Add(lowered);
            wrapper.Children.Add(gated);
        }

        // A lone Compact needs no gating — it IS the tree at every size.
        if (adaptive.Medium is null && adaptive.Expanded is null)
            return Lower(adaptive.Compact, horizontalAxis: null) ?? wrapper;

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
    private HtmlElement LowerGrid(Grid grid)
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
            if (Lower(child, horizontalAxis: null) is { } lowered)
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
    private string? GapValue(FlexNode flex, bool horizontal)
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
    /// <summary>
    /// The <c>background-image</c> layer LIST, in CSS paint order — the FIRST entry sits on top.
    /// The order (gradient → glow → grid) is the one the native realizer emits back-to-front, so a
    /// box carrying all three stacks identically on both targets.
    /// </summary>
    private string? BackgroundLayers(BoxStyle style)
    {
        var layers = new List<string>(3);
        if (style.Gradient is { } gradient) layers.Add(TokenCss.Gradient(gradient));
        if (style.Glow is { } glow) layers.Add(TokenCss.Glow(glow));
        if (style.Pattern is { } pattern) layers.Add(TokenCss.GridPattern(pattern));
        return layers.Count == 0 ? null : string.Join(", ", layers);
    }

    /// <summary>The matching <c>background-size</c> list: one entry per layer (the grid contributes
    /// TWO), with `auto` standing in for the layers that size themselves.</summary>
    private string? BackgroundLayerSizes(BoxStyle style)
    {
        if (style.Pattern is not { } pattern) return null;
        var sizes = new List<string>(3);
        if (style.Gradient is not null) sizes.Add("auto");
        if (style.Glow is not null) sizes.Add("auto");
        sizes.Add(TokenCss.GridPatternSize(pattern));
        return string.Join(", ", sizes);
    }
    private HtmlElement LowerFlexible(Flexible flexible, bool? horizontalAxis)
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
        if (Lower(flexible.Child, horizontalAxis) is { } child)
            element.Children.Add(child);
        return element;
    }

    private HtmlElement? LowerSpacer(Spacer spacer, bool? horizontalAxis)
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
    private string? Rigid(SizeValue width, SizeValue height) =>
        width.Kind == SizeKind.Fixed || height.Kind == SizeKind.Fixed ? "0" : null;
}
