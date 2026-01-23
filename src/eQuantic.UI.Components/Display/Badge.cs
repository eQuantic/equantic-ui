using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public enum BadgeVariant
{
    Primary,
    Secondary,
    Success,
    Danger,
    Warning,
    Info,
    Light,
    Dark
}

public class Badge : HtmlElement
{
    public string Text { get; set; } = string.Empty;
    public BadgeVariant Variant { get; set; } = BadgeVariant.Primary;
    public bool Pill { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = $"badge bg-{Variant.ToString().ToLowerInvariant()}";
        if (Pill) classes += " rounded-pill";
        
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";

        return new HtmlNode
        {
            Tag = "span",
            Attributes = attrs,
            Children = { HtmlNode.Text(Text) }
        };
    }
}
