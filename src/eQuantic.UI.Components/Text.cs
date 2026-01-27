using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

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
    /// Style variant
    /// </summary>
    public Variant Variant { get; set; } = Variant.Default;

    public Text() { }

    public Text(string content)
    {
        Content = content;
    }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var textTheme = theme?.Typography;

        var attrs = new Dictionary<string, string>
        {
            ["class"] = StyleBuilder.Create(textTheme?.Base)
                            .Add(textTheme?.GetVariant(Variant))
                            .Add(ClassName)
                            .Build()
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
