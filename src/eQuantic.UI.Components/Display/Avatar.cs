using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public enum AvatarSize
{
    Small,
    Medium,
    Large
}

public class Avatar : StatelessComponent
{
    public string? ImageUrl { get; set; }
    public string? Initials { get; set; }
    public string? AltText { get; set; }
    public AvatarSize Size { get; set; } = AvatarSize.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var avatarTheme = theme?.Avatar;

        var rootStyle = avatarTheme?.Root ?? "";
        var imageStyle = avatarTheme?.Image ?? "";
        var fallbackStyle = avatarTheme?.Fallback ?? "";

        // Size handling (could also be part of theme, but keeping logic here for now)
        var sizeClass = Size switch
        {
            AvatarSize.Small => "h-8 w-8",
            AvatarSize.Large => "h-14 w-14",
            _ => "h-10 w-10" // Default matches theme root, but overrides if needed
        };
        // Remove fixed size from theme root if we handle it here, or rely on theme root having default
        // The theme root has "h-10 w-10". 
        // We should append sizeClass to rootStyle.

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
