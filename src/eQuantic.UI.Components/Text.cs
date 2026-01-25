using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Text component - renders as a span or paragraph
/// </summary>
public class Text : StatelessComponent
{
    /// <summary>
    /// Text content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Whether to render as paragraph (p) instead of span
    /// </summary>
    public bool Paragraph { get; set; }
    
    /// <summary>
    /// Style variant (e.g. "large", "small", "muted", "lead")
    /// </summary>
    public string? Variant { get; set; }

    public Text() { }

    public Text(string content)
    {
        Content = content;
    }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var textTheme = theme?.Typography;
        
        var baseStyle = textTheme?.Base ?? "";
        var variantStyle = "";
        if (Variant != null && textTheme?.Variants != null && textTheme.Variants.TryGetValue(Variant.ToLower(), out var v))
        {
            variantStyle = v;
        }

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{baseStyle} {variantStyle} {ClassName}"
        };

        return new DynamicElement
        {
            TagName = Paragraph ? "p" : "span",
            InnerText = Content,
            CustomAttributes = attrs,
            CustomEvents = BuildEvents()
        };
    }
}
