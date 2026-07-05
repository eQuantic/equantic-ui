using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Web.Components;

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
        var theme = context.GetService<IAppTheme>();
        var textTheme = theme?.Typography;

        return new Box
        {
            As = Paragraph ? "p" : "span",
            ClassName = StyleBuilder.Create(textTheme?.Base)
                            .Add(textTheme?.GetVariant(Variant))
                            .Add(ClassName)
                            .Build(),
            CustomEvents = BuildEvents(),
            InnerHtml = Content
        };
    }
}
