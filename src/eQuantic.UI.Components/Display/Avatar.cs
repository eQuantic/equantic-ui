using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public enum AvatarSize
{
    Small,
    Medium,
    Large
}

public class Avatar : HtmlElement
{
    public string? ImageUrl { get; set; }
    public string? Initials { get; set; }
    public string? AltText { get; set; }
    public AvatarSize Size { get; set; } = AvatarSize.Medium;

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var sizeClass = Size switch
        {
            AvatarSize.Small => "avatar-sm",
            AvatarSize.Large => "avatar-lg",
            _ => "avatar-md"
        };

        var classes = $"avatar {sizeClass}";
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";

        if (ImageUrl != null)
        {
            return new HtmlNode
            {
                Tag = "img",
                Attributes = new Dictionary<string, string?>
                {
                    ["src"] = ImageUrl,
                    ["alt"] = AltText ?? "Avatar",
                    ["class"] = attrs["class"]
                }
            };
        }

        // Fallback to initials
        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Children = { HtmlNode.Text(Initials ?? "?") }
        };
    }
}
