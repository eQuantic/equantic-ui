using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Web.Components.Display;

public class Avatar : StatelessComponent
{
    public string? ImageUrl { get; set; }
    public string? Initials { get; set; }
    public string? AltText { get; set; }
    public SizeVariant Size { get; set; } = SizeVariant.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var avatarTheme = theme?.Avatar;

        var rootStyle = avatarTheme?.Root ?? "eq-avatar";
        var imageStyle = avatarTheme?.Image ?? "eq-avatar-image";
        var fallbackStyle = avatarTheme?.Fallback ?? "eq-avatar-fallback";

        // Get size from theme
        var sizeClass = avatarTheme?.GetSize(Size) ?? $"eq-avatar-{Size.ToString().ToLowerInvariant()}";

        var rootElement = new Box
        {
            As = "div",
            ClassName = $"{rootStyle} {sizeClass} {ClassName}".Trim()
        };

        if (ImageUrl != null)
        {
            rootElement.Children.Add(new Box
            {
                As = "img",
                Src = ImageUrl,
                Alt = AltText ?? "Avatar",
                ClassName = imageStyle
            });
        }
        else
        {
            rootElement.Children.Add(new Box
            {
                As = "div",
                ClassName = fallbackStyle,
                Children = { new Text(Initials ?? "?") }
            });
        }

        return rootElement;
    }
}
