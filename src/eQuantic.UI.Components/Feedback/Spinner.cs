using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Feedback;

public enum SpinnerVariant
{
    Border,
    Grow
}

public enum SpinnerSize
{
    Small,
    Normal
}

public class Spinner : HtmlElement
{
    public SpinnerVariant Variant { get; set; } = SpinnerVariant.Border;
    public SpinnerSize Size { get; set; } = SpinnerSize.Normal;
    public string? Color { get; set; }
    public string? Label { get; set; } = "Loading...";

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var typeClass = Variant == SpinnerVariant.Border ? "spinner-border" : "spinner-grow";
        var classes = typeClass;
        
        if (Size == SpinnerSize.Small) classes += $" {typeClass}-sm";
        if (Color != null) classes += $" text-{Color}";
        
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";
        attrs["role"] = "status";

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Children = {
                new HtmlNode
                {
                    Tag = "span",
                    Attributes = new Dictionary<string, string?> { ["class"] = "visually-hidden" },
                    Children = { HtmlNode.Text(Label ?? "") }
                }
            }
        };
    }
}
