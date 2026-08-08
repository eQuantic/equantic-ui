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
/// Layout parity notes (v1): flex maps 1:1 (direction/gap/justify/align, Flexible → <c>flex: n 1 0%</c>
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

    private static HtmlElement? LowerNode(VisualNode node, ComponentContext context, bool? horizontalAxis) => node switch
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
        Spinner spinner => LowerSpinner(spinner, context),
        Primitives.Image image => LowerImage(image),
        CameraPreview camera => LowerCameraPreview(camera),
        WebFrame frame => LowerWebFrame(frame),
        Pressable pressable => LowerPressable(pressable, context),
        Hoverable hoverable => LowerHoverable(hoverable, context),
        Adjustable adjustable => LowerAdjustable(adjustable, context),
        Shortcut shortcut => LowerShortcut(shortcut, context, horizontalAxis),
        Link link => LowerLink(link, context),
        LoopMotion motion => LowerLoopMotion(motion, context),
        Presence presence => LowerPresence(presence, context, horizontalAxis),
        DragDismiss drag => LowerDragDismiss(drag, context, horizontalAxis),
        Draggable draggable => LowerDraggable(draggable, context, horizontalAxis),
        Flexible flexible => LowerFlexible(flexible, context, horizontalAxis),
        Spacer spacer => LowerSpacer(spacer, horizontalAxis),
        UiComponent component => LowerNode(component.Build(context), context, horizontalAxis),
        _ => null,
    };

    /// <summary>
    /// Spec A3 lowering: single-cell CSS grid — every NON-positioned child sits in cell 1/1
    /// (overlapping, painted in child order) with <c>place-items</c> carrying the alignment;
    /// Positioned children wrap in <c>position:absolute</c> against the stack's
    /// <c>position:relative</c> frame, with signed offsets (End→right, Start→left).
    /// </summary>
    private static HtmlElement LowerStack(Stack stack, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Display = Core.Display.Grid,
                Position = Core.Position.Relative,
                Width = Size(stack.Width),
                Height = Size(stack.Height),
            },
        };

        // Spec A3: paint order IS child order. On web that needs an EXPLICIT z-index per cell —
        // a child carrying backdrop-filter/filter/opacity creates a stacking context, which CSS
        // paints as if positioned at z-index 0, i.e. ABOVE every plain sibling that follows it in
        // the DOM. (A blurred scrim would otherwise cover the dialog it sits behind.)
        var depth = 0;
        foreach (var child in stack.Children)
        {
            depth++;
            if (child is Positioned positioned)
            {
                var lowered = LowerNode(positioned.Child, context, horizontalAxis: null);
                if (lowered is null) continue;
                var anchor = new RealizedElement("div")
                {
                    Style = new HtmlStyle
                    {
                        Position = Core.Position.Absolute,
                        Top = positioned.Top is { } top ? TokenCss.Px(top) : null,
                        Right = positioned.End is { } end ? TokenCss.Px(end) : null,
                        Bottom = positioned.Bottom is { } bottom ? TokenCss.Px(bottom) : null,
                        Left = positioned.Start is { } start ? TokenCss.Px(start) : null,
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
                        // A grid item takes z-index without needing `position` — this is what keeps
                        // a filtered child from jumping above the siblings drawn after it.
                        ZIndex = depth.ToString(),
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
        if (LowerNode(anchored.Anchor, context, horizontalAxis: null) is { } anchor)
        {
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
            host.Children.Add(BuildAnchorPanel(anchored, context));
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

        host.Children.Add(BuildAnchorPanel(anchored, context));
        return host;
    }

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
                ZIndex = sticky.Float ? "100" : "1",
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
                Width = Size(scroll.Width),
                // A scroll view IS the window its parent gives it — never its content. Native gets
                // that from layout (bounds in, clip always); an auto-height overflow div grows with
                // the content instead and scrolls nothing, so the web mirror defaults to 100%.
                Height = Size(scroll.Height) ?? "100%",
                OverflowY = scroll.Axis is ScrollAxis.Vertical or ScrollAxis.Both ? "auto" : "hidden",
                OverflowX = scroll.Axis is ScrollAxis.Horizontal or ScrollAxis.Both ? "auto" : "hidden",
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

    /// <summary>A vector shape lowers exactly like an icon — the only difference is that its size
    /// is the author's, not the §07 whitelist's.</summary>
    private static HtmlElement LowerVector(Vector vector) =>
        LowerGlyph(vector.Glyph, vector.Size, vector.Color, vector.Label);

    private static HtmlElement LowerIcon(Icon icon, ComponentContext context)
        => LowerGlyph(icon.Glyph, icon.Size, icon.Color, icon.Label);

    private static HtmlElement LowerGlyph(IconGlyph glyph, float size, ColorToken? color, string? label)
    {
        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                Width = TokenCss.Px(size),
                Height = TokenCss.Px(size),
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
        if (label is { }) svg.RawAttributes["aria-label"] = label;
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
    /// SSR emits no events; hydration wires them).
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
        // The attribute alone only acts on the initial parse; the client lowering also focuses on
        // mount, which is what a dialog opened later needs.
        if (entry.Autofocus) element.RawAttributes["autofocus"] = "";
        return element;
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
        var fillsWidth = Fills(adjustable.Child).Width;
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fillsWidth ? "100%" : null,
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
        if (adjustable.Label.Length > 0) element.RawAttributes["aria-label"] = adjustable.Label;
        if (LowerNode(adjustable.Child, context, null) is { } child) element.Children.Add(child);
        return element;
    }

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
        return element;
    }

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
                Height = Size(style.Height),
                MinWidth = style.MinWidth > 0 ? TokenCss.Px(style.MinWidth) : null,
                MinHeight = style.MinHeight > 0 ? TokenCss.Px(style.MinHeight) : null,
                MaxWidth = style.MaxWidth > 0 ? TokenCss.Px(style.MaxWidth) : null,
                MaxHeight = style.MaxHeight > 0 ? TokenCss.Px(style.MaxHeight) : null,
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
                Border = style.BorderWidth > 0
                    ? $"{TokenCss.Px(style.BorderWidth)} solid {TokenCss.Value(style.BorderColor)}"
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
            },
        };

        if (box.Style.Hover is { IsEmpty: false } hover)
            AppendDiff(element, ":hover", hover, context);
        if (box.Style.Focus is { IsEmpty: false } focus)
            AppendDiff(element, ":focus-visible", focus, context);

        if (box.Child is not null && LowerNode(box.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
    }

    /// <summary>One box-shadow list from the optional parts (null when none are set).</summary>
    private static string? ComposeShadows(params string?[] parts)
    {
        var present = parts.Where(part => part != null).ToList();
        return present.Count == 0 ? null : string.Join(", ", present);
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
                Height = Size(flex.Height),
                Padding = flex.Padding == EdgeInsets.Zero ? null : TokenCss.Padding(flex.Padding),
                BackgroundColor = flex.Background is { } bg ? TokenCss.Value(bg) : null,
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
                Height = Size(grid.Height),
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
        var element = new RealizedElement("span")
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
                WhiteSpace = mono ? "pre-wrap"
                    : text.Content.Contains('\n') ? "pre-line" : null,
                FontFamily = mono ? TokenCss.MonoStack : null,
                FontVariantNumeric = text.Tabular ? "tabular-nums" : null,
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
                element.Children.Add(new RealizedElement("span")
                {
                    InnerHtml = run.Content,
                    Style = new HtmlStyle
                    {
                        Color = run.Color is { } runColor ? TokenCss.Value(runColor) : null,
                        FontFamily = run.Mono ? TokenCss.MonoStack : null,
                    },
                });
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
            element.Style!.WhiteSpace = "nowrap";
            element.Style.Overflow = "hidden";
            element.Style.TextOverflow = "ellipsis";
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
    /// Whether a lowered subtree already carries an interactive ELEMENT. HTML forbids a button
    /// inside a button (and an anchor inside an anchor): the parser closes the outer one and hands
    /// back an empty shell, so the wrapper renders as nothing and the hydrated tree disagrees with
    /// the served HTML about the whole subtree.
    /// </summary>
    private static bool WrapsAnInteractive(IComponent node) =>
        node is RealizedElement element
        && (element.Tag is "button" or "a" || element.Children.Any(WrapsAnInteractive));

    private static HtmlElement LowerPressable(Pressable pressable, ComponentContext context)
    {
        var fills = Fills(pressable.Child);
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
                Height = fills.Height ? "100%" : null,
            },
            Disabled = pressable.Disabled && !wrapping ? true : null,
            AriaLabel = pressable.Label,
            // Selection stated, not merely painted — a fill colour says nothing to a screen reader.
            AriaPressed = pressable.Selected is { } selected ? (selected ? "true" : "false") : null,
            OnClick = pressable.Disabled ? null : pressable.OnPressed,
        };

        // Interaction states (spec §01): mechanics live in the GENERATED stylesheet — every enabled
        // pressable carries the class (the :focus-visible double ring is an accessibility DEFAULT);
        // the pressed swap additionally needs its token value as a per-element custom property.
        if (!pressable.Disabled)
        {
            element.ClassName = "eq-pressable";
            if (pressable.PressedBackground is { } pressedFill)
            {
                element.Style!.CustomProperties = new Dictionary<string, string>
                {
                    ["--eq-pressed-bg"] = TokenCss.Value(pressedFill),
                };
            }
        }

        // A span that acts as a button says so, and takes the keyboard the same way.
        if (wrapping)
        {
            element.Role = "button";
            if (pressable.Disabled) element.AriaDisabled = true;
            else element.TabIndex = 0;
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
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : null,
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
    /// Navigation semantics: a REAL <c>&lt;a href&gt;</c> (SSR-crawlable, router-intercepted) whose
    /// UA chrome is neutralized — the child owns all visuals, exactly the Pressable contract. A Fill
    /// child gets the 100% pass-through chain the same way.
    /// </summary>
    private static HtmlElement LowerLink(Link link, ComponentContext context)
    {
        var fills = Fills(link.Child);
        var element = new RealizedElement("a")
        {
            ClassName = "eq-link",
            Style = new HtmlStyle
            {
                Width = fills.Width ? "100%" : null,
                Height = fills.Height ? "100%" : null,
            },
            AriaLabel = link.Label,
            RawAttributes = new Dictionary<string, string> { ["href"] = link.Href },
        };
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
        // flex: n 1 0% — basis 0 matches the native engine's leftover-by-weight distribution; min-size 0
        // lets text children shrink to ellipsis instead of pushing siblings (the truncation contract).
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                // Spec B14 value transitions: weight changes animate Base/standard; the component
                // omits the flag on a regression so the change SNAPS (forward-only honesty).
                Transition = flexible.AnimateChanges
                    ? "flex-grow var(--eq-motion-base) var(--eq-curve-standard)"
                    : null,
                Flex = $"{flexible.Flex} 1 0%",
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

    private static string? Size(SizeValue value) => value.Kind switch
    {
        SizeKind.Fixed => TokenCss.Px(value.Value),
        SizeKind.Fill => "100%",
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
