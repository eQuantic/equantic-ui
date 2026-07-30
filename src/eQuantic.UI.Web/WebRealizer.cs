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
        ScrollView scroll => LowerScrollView(scroll, context),
        // A Positioned outside a Stack has no anchor frame — degrade to its child (parity with native).
        Positioned positioned => LowerNode(positioned.Child, context, horizontalAxis),
        Text text => LowerText(text, context),
        TextEntry entry => LowerTextEntry(entry, context),
        Overlay overlay => LowerOverlay(overlay, context),
        Icon icon => LowerIcon(icon, context),
        Spinner spinner => LowerSpinner(spinner, context),
        Primitives.Image image => LowerImage(image),
        Pressable pressable => LowerPressable(pressable, context),
        LoopMotion motion => LowerLoopMotion(motion, context),
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

        foreach (var child in stack.Children)
        {
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
                        // Spec S7: explicit stacking — flow order otherwise (painter's parity).
                        ZIndex = positioned.ZIndex != 0 ? positioned.ZIndex.ToString() : null,
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
    private static HtmlElement LowerSticky(Sticky sticky, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Position = Core.Position.Sticky,
                Top = TokenCss.Px(sticky.Offset),
                // Pinned chrome floats over the content it sticks above.
                ZIndex = "1",
            },
        };
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
                Height = Size(scroll.Height),
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

    private static HtmlElement LowerIcon(Icon icon, ComponentContext context)
    {
        var glyph = icon.Glyph;
        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                Width = TokenCss.Px(icon.Size),
                Height = TokenCss.Px(icon.Size),
                Color = icon.Color is { } tint ? TokenCss.Value(tint) : null,
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
        if (icon.Label is { } label) svg.RawAttributes["aria-label"] = label;
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
    private static HtmlElement LowerOverlay(Overlay overlay, ComponentContext context)
    {
        var element = new RealizedElement("div")
        {
            ClassName = overlay.Modal ? "eq-overlay" : "eq-overlay eq-overlay-passthrough",
        };
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
        var element = new RealizedElement("input")
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
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["type"] = entry.Obscure ? "password" : "text",
                ["value"] = entry.Value,
            },
        };
        if (entry.Placeholder is { } placeholder) element.RawAttributes["placeholder"] = placeholder;
        if (entry.Disabled) element.RawAttributes["disabled"] = "";
        return element;
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
                BackgroundImage = style.Gradient is { } gradient ? TokenCss.Gradient(gradient) : null,
                BorderRadius = style.CornerRadius.IsZero ? null : TokenCss.Radius(style.CornerRadius),
                BoxShadow = style.Elevation > 0 && !context.Theme.Elevation(style.Elevation).IsNone
                    ? TokenCss.Shadow(context.Theme.Elevation(style.Elevation))
                    : null,
                Border = style.BorderWidth > 0
                    ? $"{TokenCss.Px(style.BorderWidth)} solid {TokenCss.Value(style.BorderColor)}"
                    : null,
                // The container side of loop motion: children clip to the rrect (native PushClip twin).
                Overflow = style.Clip ? "hidden" : null,
                // Spec S1 — group opacity (native PushLayer twin), the center-anchored static
                // transform (paint-only), and the one-axis-derives aspect ratio.
                Opacity = style.Opacity is { } alpha && alpha < 1f ? TokenCss.Number(alpha) : null,
                Transform = style.Transform is { } transform ? TokenCss.Transform(transform) : null,
                AspectRatio = style.AspectRatio > 0 ? TokenCss.Number(style.AspectRatio) : null,
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

        AddVariant(adaptive.Compact, adaptive.Medium is not null
            ? AdaptiveGates.CompactUntilMedium
            : AdaptiveGates.CompactUntilExpanded);
        if (adaptive.Medium is { } medium)
            AddVariant(medium, adaptive.Expanded is not null ? AdaptiveGates.MediumUntilExpanded : AdaptiveGates.MediumOpen);
        if (adaptive.Expanded is { } expanded)
            AddVariant(expanded, AdaptiveGates.Expanded);
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
        var element = new RealizedElement("span")
        {
            ClassName = $"eq-type-{text.Role.ToString().ToLowerInvariant()}",
            InnerHtml = text.Content,
            Style = new HtmlStyle
            {
                Color = TokenCss.Value(text.Color ?? context.Theme.TextPrimary),
            },
        };

        // Single line → shaping-style ellipsis (spec A8). Multi-line clamp joins with the TS lowering.
        if (text.MaxLines == 1)
        {
            element.Style!.WhiteSpace = "nowrap";
            element.Style.Overflow = "hidden";
            element.Style.TextOverflow = "ellipsis";
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
    private static (bool Width, bool Height) Fills(VisualNode node) => node switch
    {
        Box box => (box.Style.Width.Kind == SizeKind.Fill, box.Style.Height.Kind == SizeKind.Fill),
        FlexNode flex => (flex.Width.Kind == SizeKind.Fill, flex.Height.Kind == SizeKind.Fill),
        Stack stack => (stack.Width.Kind == SizeKind.Fill, stack.Height.Kind == SizeKind.Fill),
        Pressable pressable => Fills(pressable.Child),
        Flexible flexible => Fills(flexible.Child),
        LoopMotion motion => Fills(motion.Child),
        _ => (false, false),
    };

    private static HtmlElement LowerPressable(Pressable pressable, ComponentContext context)
    {
        var fills = Fills(pressable.Child);
        var element = new RealizedElement("button")
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
            Disabled = pressable.Disabled ? true : null,
            AriaLabel = pressable.Label,
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

        if (LowerNode(pressable.Child, context, horizontalAxis: null) is { } child)
            element.Children.Add(child);
        return element;
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

        var style = spacer.Flex > 0
            ? new HtmlStyle { Flex = $"{spacer.Flex} 1 0%" }
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
internal sealed class RealizedElement : HtmlElement, IPseudoStyled, IAdaptiveGated
{
    /// <summary>Spec S6: the size-class gate this element carries (fixed class + media blob).</summary>
    public string? AdaptiveGate { get; set; }

    public RealizedElement(string tag) => Tag = tag;

    public string Tag { get; }

    /// <summary>Spec S5: hover/focus diff declarations, converted to pseudo-variant atomic rules by
    /// the atomizer pass (pseudo-classes need the ATOMIC pipeline — inline styles can't express them).</summary>
    public List<(string Pseudo, string Prop, string Value)> PseudoDeclarations { get; } = new();

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
