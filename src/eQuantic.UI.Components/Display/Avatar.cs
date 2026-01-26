using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Display;

public class Avatar : StatelessComponent
{
    public string? ImageUrl { get; set; }
    public string? Initials { get; set; }
    public string? AltText { get; set; }
    public Size Size { get; set; } = Size.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var avatarTheme = theme?.Avatar;

        var rootStyle = avatarTheme?.Root ?? "";
        var imageStyle = avatarTheme?.Image ?? "";
        var fallbackStyle = avatarTheme?.Fallback ?? "";

        // Get size from theme
        var sizeClass = avatarTheme?.GetSize(Size) ?? "";

        var rootElement = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{rootStyle} {sizeClass} {ClassName}".Trim()
            }
        };

        if (ImageUrl != null)
        {
            rootElement.Children.Add(new DynamicElement
            {
                TagName = "img",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["src"] = ImageUrl,
                    ["alt"] = AltText ?? "Avatar",
                    ["class"] = imageStyle
                }
            });
        }
        else
        {
            rootElement.Children.Add(new DynamicElement
            {
                TagName = "div",
                CustomAttributes = new Dictionary<string, string> { ["class"] = fallbackStyle },
                Children = { new Text(Initials ?? "?") }
            });
        }

        return rootElement;
    }
}
