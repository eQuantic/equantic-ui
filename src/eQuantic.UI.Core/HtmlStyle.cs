using System.Collections.Generic;

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

    #endregion

    #region Border

    public string? Border { get; set; }
    public string? BorderWidth { get; set; }
    public string? BorderStyle { get; set; }
    public string? BorderColor { get; set; }
    public string? BorderRadius { get; set; }

    #endregion

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
    public string? Opacity { get; set; }
    public string? Cursor { get; set; }
    public string? Overflow { get; set; }
    public string? OverflowX { get; set; }
    public string? OverflowY { get; set; }
    public string? Transition { get; set; }
    public string? Transform { get; set; }

    #endregion

    /// <summary>
    /// Convert to CSS string for inline styles
    /// </summary>
    public string ToCssString()
    {
        var properties = new List<string>();

        AddProperty(properties, "display", Display);
        AddProperty(properties, "position", Position);
        AddProperty(properties, "top", Top);
        AddProperty(properties, "right", Right);
        AddProperty(properties, "bottom", Bottom);
        AddProperty(properties, "left", Left);
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

        AddProperty(properties, "border", Border);
        AddProperty(properties, "border-width", BorderWidth);
        AddProperty(properties, "border-style", BorderStyle);
        AddProperty(properties, "border-color", BorderColor);
        AddProperty(properties, "border-radius", BorderRadius);

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
        AddProperty(properties, "opacity", Opacity);
        AddProperty(properties, "cursor", Cursor);
        AddProperty(properties, "overflow", Overflow);
        AddProperty(properties, "overflow-x", OverflowX);
        AddProperty(properties, "overflow-y", OverflowY);
        AddProperty(properties, "transition", Transition);
        AddProperty(properties, "transform", Transform);

        return string.Join("; ", properties);
    }

    private static void AddProperty(List<string> properties, string name, object? value)
    {
        if (value != null)
        {
            var cssValue = value.ToString()!.ToLowerInvariant();

            // Fix generic enum replacements
            cssValue = cssValue
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
                .Replace("rowdense", "row dense")
                .Replace("columndense", "column dense")
                .Replace("nowrap", "nowrap"); // Enum is NoWrap, CSS is nowrap

            properties.Add($"{name}: {cssValue}");
        }
    }
}
