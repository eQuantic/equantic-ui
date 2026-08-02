using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Core;

/// <summary>
/// Inline CSS styles with type-safe properties
/// </summary>
public class HtmlStyle
{
    #region Layout

    public Display? Display { get; set; }
    public Position? Position { get; set; }
    public string? Top { get; set; }
    public string? Right { get; set; }
    public string? Bottom { get; set; }
    public string? Left { get; set; }
    public string? ZIndex { get; set; }

    #endregion

    #region Flexbox

    public FlexDirection? FlexDirection { get; set; }
    public FlexWrap? FlexWrap { get; set; }
    public JustifyContent? JustifyContent { get; set; }
    public AlignItem? AlignItems { get; set; }
    public AlignItem? AlignContent { get; set; }
    public string? Gap { get; set; }
    public string? Flex { get; set; }
    public string? FlexGrow { get; set; }
    public string? FlexShrink { get; set; }
    public string? AlignSelf { get; set; }

    #endregion

    #region Grid

    public string? GridTemplateColumns { get; set; }
    public string? GridTemplateRows { get; set; }
    public string? GridColumn { get; set; }
    public string? GridRow { get; set; }
    public GridFlow? GridAutoFlow { get; set; }
    public JustifyContent? JustifyItems { get; set; }

    #endregion

    #region Sizing

    public string? Width { get; set; }
    public string? Height { get; set; }
    public string? AspectRatio { get; set; }
    public string? MinWidth { get; set; }
    public string? MinHeight { get; set; }
    public string? MaxWidth { get; set; }
    public string? MaxHeight { get; set; }

    #endregion

    #region Spacing

    public string? Margin { get; set; }
    public string? MarginTop { get; set; }
    public string? MarginRight { get; set; }
    public string? MarginBottom { get; set; }
    public string? MarginLeft { get; set; }

    public string? Padding { get; set; }
    public string? PaddingTop { get; set; }
    public string? PaddingRight { get; set; }
    public string? PaddingBottom { get; set; }
    public string? PaddingLeft { get; set; }

    #endregion

    #region Background

    public string? Background { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BackgroundImage { get; set; }
    public string? BackgroundSize { get; set; }
    public string? BackgroundPosition { get; set; }

    /// <summary>`background-clip` — `text` paints the background through the glyphs (gradient text).
    /// The `-webkit-` twin is emitted alongside it: Safari still requires the prefix.</summary>
    public string? BackgroundClip { get; set; }

    /// <summary>`-webkit-text-fill-color` — set to `transparent` so the clipped background shows
    /// through. Prefixed-only by specification; there is no unprefixed equivalent.</summary>
    public string? WebkitTextFillColor { get; set; }

    public string? Filter { get; set; }

    #endregion

    #region Border

    public string? Border { get; set; }
    public string? BorderWidth { get; set; }
    public string? BorderStyle { get; set; }
    public string? BorderColor { get; set; }
    public string? BorderRadius { get; set; }

    #endregion

    public string? ObjectFit { get; set; }

    /// <summary>CSS custom properties (<c>--name: value</c>), emitted AFTER every ordered property —
    /// per-element inputs for generated-stylesheet mechanics (e.g. the pressed-state swap).</summary>
    public Dictionary<string, string>? CustomProperties { get; set; }
    public string? ObjectPosition { get; set; }

    #region Typography

    public string? Color { get; set; }
    public string? FontFamily { get; set; }
    public string? FontSize { get; set; }
    public string? FontWeight { get; set; }
    public string? FontStyle { get; set; }
    public string? LineHeight { get; set; }
    public TextAlign? TextAlign { get; set; }
    public string? TextDecoration { get; set; }
    public string? TextTransform { get; set; }
    public string? LetterSpacing { get; set; }

    #endregion

    #region Effects

    public string? BoxShadow { get; set; }

    /// <summary>Emitted as BOTH <c>backdrop-filter</c> and <c>-webkit-backdrop-filter</c> — Safari
    /// still requires the prefix, and the atomizer dedupes each declaration independently.</summary>
    public string? BackdropFilter { get; set; }

    /// <summary>Multi-line clamp: the axis the legacy flexbox lays out on (`vertical`).</summary>
    public string? WebkitBoxOrient { get; set; }

    /// <summary>Multi-line clamp: how many lines survive before the ellipsis.</summary>
    public string? WebkitLineClamp { get; set; }

    public string? Opacity { get; set; }
    public string? Visibility { get; set; }
    public string? PointerEvents { get; set; }
    public string? Cursor { get; set; }
    public string? Overflow { get; set; }
    public string? OverflowX { get; set; }
    public string? OverflowY { get; set; }
    public string? Transition { get; set; }
    public string? Transform { get; set; }
    public string? Animation { get; set; }
    public string? AnimationDelay { get; set; }
    public string? WhiteSpace { get; set; }
    public string? TextOverflow { get; set; }
    public string? BoxSizing { get; set; }

    public string? PlaceItems { get; set; }

    public string? GridArea { get; set; }

    #endregion

    /// <summary>
    /// The style's REGULAR declarations as ordered (property, value) pairs — the seam the web
    /// realizer's StyleAtomizer consumes (each pair becomes one deduplicated atomic class).
    /// Custom properties are deliberately NOT included: they are per-element inputs (tier 3 of the
    /// style pipeline) and stay inline; read them from <see cref="CustomProperties"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> EnumerateDeclarations()
    {
        var properties = new List<KeyValuePair<string, string>>();

        AddProperty(properties, "display", Display);
        AddProperty(properties, "position", Position);
        AddProperty(properties, "top", Top);
        AddProperty(properties, "right", Right);
        AddProperty(properties, "bottom", Bottom);
        AddProperty(properties, "left", Left);
        AddProperty(properties, "place-items", PlaceItems);
        AddProperty(properties, "grid-area", GridArea);
        AddProperty(properties, "z-index", ZIndex);

        AddProperty(properties, "flex-direction", FlexDirection);
        AddProperty(properties, "flex-wrap", FlexWrap);
        AddProperty(properties, "justify-content", JustifyContent);
        AddProperty(properties, "align-items", AlignItems);
        AddProperty(properties, "align-content", AlignContent);
        AddProperty(properties, "gap", Gap);
        AddProperty(properties, "flex", Flex);
        AddProperty(properties, "flex-grow", FlexGrow);
        AddProperty(properties, "flex-shrink", FlexShrink);
        AddProperty(properties, "align-self", AlignSelf);

        AddProperty(properties, "grid-template-columns", GridTemplateColumns);
        AddProperty(properties, "grid-template-rows", GridTemplateRows);
        AddProperty(properties, "grid-column", GridColumn);
        AddProperty(properties, "grid-row", GridRow);
        AddProperty(properties, "grid-auto-flow", GridAutoFlow);
        AddProperty(properties, "justify-items", JustifyItems);

        AddProperty(properties, "width", Width);
        AddProperty(properties, "height", Height);
        AddProperty(properties, "min-width", MinWidth);
        AddProperty(properties, "min-height", MinHeight);
        AddProperty(properties, "max-width", MaxWidth);
        AddProperty(properties, "max-height", MaxHeight);
        AddProperty(properties, "aspect-ratio", AspectRatio);

        AddProperty(properties, "margin", Margin);
        AddProperty(properties, "margin-top", MarginTop);
        AddProperty(properties, "margin-right", MarginRight);
        AddProperty(properties, "margin-bottom", MarginBottom);
        AddProperty(properties, "margin-left", MarginLeft);

        AddProperty(properties, "padding", Padding);
        AddProperty(properties, "padding-top", PaddingTop);
        AddProperty(properties, "padding-right", PaddingRight);
        AddProperty(properties, "padding-bottom", PaddingBottom);
        AddProperty(properties, "padding-left", PaddingLeft);

        AddProperty(properties, "background", Background);
        AddProperty(properties, "background-color", BackgroundColor);
        AddProperty(properties, "background-image", BackgroundImage);
        AddProperty(properties, "background-size", BackgroundSize);
        AddProperty(properties, "background-position", BackgroundPosition);
        AddProperty(properties, "-webkit-background-clip", BackgroundClip);
        AddProperty(properties, "background-clip", BackgroundClip);
        AddProperty(properties, "-webkit-text-fill-color", WebkitTextFillColor);
        AddProperty(properties, "filter", Filter);

        AddProperty(properties, "border", Border);
        AddProperty(properties, "border-width", BorderWidth);
        AddProperty(properties, "border-style", BorderStyle);
        AddProperty(properties, "border-color", BorderColor);
        AddProperty(properties, "border-radius", BorderRadius);

        AddProperty(properties, "object-fit", ObjectFit);
        AddProperty(properties, "object-position", ObjectPosition);

        AddProperty(properties, "color", Color);
        AddProperty(properties, "font-family", FontFamily);
        AddProperty(properties, "font-size", FontSize);
        AddProperty(properties, "font-weight", FontWeight);
        AddProperty(properties, "font-style", FontStyle);
        AddProperty(properties, "line-height", LineHeight);
        AddProperty(properties, "text-align", TextAlign);
        AddProperty(properties, "text-decoration", TextDecoration);
        AddProperty(properties, "text-transform", TextTransform);
        AddProperty(properties, "letter-spacing", LetterSpacing);

        AddProperty(properties, "box-shadow", BoxShadow);
        AddProperty(properties, "-webkit-box-orient", WebkitBoxOrient);
        AddProperty(properties, "-webkit-line-clamp", WebkitLineClamp);
        AddProperty(properties, "opacity", Opacity);
        AddProperty(properties, "visibility", Visibility);
        AddProperty(properties, "pointer-events", PointerEvents);
        AddProperty(properties, "backdrop-filter", BackdropFilter);
        AddProperty(properties, "-webkit-backdrop-filter", BackdropFilter);
        AddProperty(properties, "cursor", Cursor);
        AddProperty(properties, "overflow", Overflow);
        AddProperty(properties, "overflow-x", OverflowX);
        AddProperty(properties, "overflow-y", OverflowY);
        AddProperty(properties, "transition", Transition);
        AddProperty(properties, "transform", Transform);
        AddProperty(properties, "animation", Animation);
        AddProperty(properties, "animation-delay", AnimationDelay);
        AddProperty(properties, "white-space", WhiteSpace);
        AddProperty(properties, "text-overflow", TextOverflow);
        AddProperty(properties, "box-sizing", BoxSizing);

        return properties;
    }

    /// <summary>
    /// Convert to CSS string for inline styles
    /// </summary>
    public string ToCssString()
    {
        var properties = EnumerateDeclarations()
            .Select(p => $"{p.Key}: {p.Value}")
            .ToList();

        // Custom properties come LAST — per-element inputs for generated-stylesheet mechanics
        // (e.g. --eq-pressed-bg); the tail position is part of the hydration cross-pin.
        if (CustomProperties != null)
        {
            foreach (var custom in CustomProperties)
                properties.Add($"{custom.Key}: {custom.Value}");
        }

        return string.Join("; ", properties);
    }

    private static void AddProperty(List<KeyValuePair<string, string>> properties, string name, object? value)
    {
        if (value == null) return;

        string cssValue;
        if (value is Enum)
        {
            // Enum member names (e.g. SpaceBetween) map to lowercase, hyphenated CSS keywords.
            cssValue = value.ToString()!.ToLowerInvariant()
                .Replace("flexstart", "flex-start")
                .Replace("flexend", "flex-end")
                .Replace("inlineblock", "inline-block")
                .Replace("inlineflex", "inline-flex")
                .Replace("inlinegrid", "inline-grid")
                .Replace("spacebetween", "space-between")
                .Replace("spacearound", "space-around")
                .Replace("spaceevenly", "space-evenly")
                .Replace("rowreverse", "row-reverse")
                .Replace("columnreverse", "column-reverse")
                .Replace("wrapreverse", "wrap-reverse")
                .Replace("rowdense", "row dense")
                .Replace("columndense", "column dense")
                .Replace("webkitbox", "-webkit-box");
        }
        else
        {
            // Arbitrary values — URLs, data: URIs, base64, custom properties — are
            // case-sensitive and must be preserved verbatim (never lowercased).
            cssValue = value.ToString()!;
        }

        properties.Add(new KeyValuePair<string, string>(name, cssValue));
    }
}
