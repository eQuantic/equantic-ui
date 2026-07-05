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
    {
        var context = new ComponentContext(theme, typeScale);
        return LowerNode(node, context, horizontalAxis: null)
               ?? new RealizedElement("span"); // layout-only nodes outside a flex row lower to nothing
    }

    private static HtmlElement? LowerNode(VisualNode node, ComponentContext context, bool? horizontalAxis) => node switch
    {
        Box box => LowerBox(box, context),
        FlexNode flex => LowerFlex(flex, context),
        Stack stack => LowerStack(stack, context),
        // A Positioned outside a Stack has no anchor frame — degrade to its child (parity with native).
        Positioned positioned => LowerNode(positioned.Child, context, horizontalAxis),
        Text text => LowerText(text, context),
        Icon icon => LowerIcon(icon, context),
        Primitives.Image image => LowerImage(image),
        Pressable pressable => LowerPressable(pressable, context),
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
                PlaceItems = PlaceItems(stack.Align),
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
                    },
                };
                anchor.Children.Add(lowered);
                element.Children.Add(anchor);
            }
            else
            {
                var lowered = LowerNode(child, context, horizontalAxis: null);
                if (lowered is null) continue;
                var cell = new RealizedElement("div") { Style = new HtmlStyle { GridArea = "1 / 1" } };
                cell.Children.Add(lowered);
                element.Children.Add(cell);
            }
        }

        return element;
    }

    /// <summary>CSS <c>place-items</c> = "&lt;align&gt; &lt;justify&gt;" (vertical then horizontal).</summary>
    private static string PlaceItems(Alignment align)
    {
        var vertical = ((int)align / 3) switch { 1 => "center", 2 => "end", _ => "start" };
        var horizontal = ((int)align % 3) switch { 1 => "center", 2 => "end", _ => "start" };
        return $"{vertical} {horizontal}";
    }

    /// <summary>
    /// Spec A10 lowering: inline 24×24-viewBox SVG with the registry's single alpha-mask path and
    /// <c>fill="currentColor"</c> — the tint rides the CSS <c>color</c> property exactly like text
    /// (token → light-dark()). Null color inherits; null label = decorative (aria-hidden).
    /// </summary>
    private static HtmlElement LowerIcon(Icon icon, ComponentContext context)
    {
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
            ["viewBox"] = "0 0 24 24",
            ["fill"] = "currentColor",
        };
        if (icon.Label is { } label) svg.RawAttributes["aria-label"] = label;
        else svg.RawAttributes["aria-hidden"] = "true";

        var glyphPath = new RealizedElement("path");
        glyphPath.RawAttributes = new Dictionary<string, string> { ["d"] = IconRegistry.Path(icon.Glyph) };
        svg.Children.Add(glyphPath);
        return svg;
    }

    /// <summary>Spec A11 lowering: an explicitly sized <c>&lt;img&gt;</c> with object-fit and the
    /// rrect clip via border-radius; empty alt = decorative (HTML semantics).</summary>
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
                BorderRadius = style.CornerRadius.IsZero ? null : TokenCss.Radius(style.CornerRadius),
                Border = style.BorderWidth > 0
                    ? $"{TokenCss.Px(style.BorderWidth)} solid {TokenCss.Value(style.BorderColor)}"
                    : null,
            },
        };

        if (box.Child is not null && LowerNode(box.Child, context, horizontalAxis: null) is { } child)
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
                Gap = flex.Gap > 0 ? TokenCss.Px(flex.Gap) : null,
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
                element.Children.Add(lowered);
        }
        return element;
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

    private static HtmlElement LowerPressable(Pressable pressable, ComponentContext context)
    {
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
            },
            Disabled = pressable.Disabled ? true : null,
            AriaLabel = pressable.Label,
            OnClick = pressable.Disabled ? null : pressable.OnPressed,
        };

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
internal sealed class RealizedElement : HtmlElement
{
    public RealizedElement(string tag) => Tag = tag;

    public string Tag { get; }

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
