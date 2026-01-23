using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Feedback;

public enum AlertVariant
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

public class Alert : HtmlElement
{
    public string? Message { get; set; }
    public AlertVariant Variant { get; set; } = AlertVariant.Info;
    public bool Dismissible { get; set; }
    public Action? OnDismiss { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = $"alert alert-{Variant.ToString().ToLowerInvariant()}";
        if (Dismissible) classes += " alert-dismissible fade show";
        attrs["role"] = "alert";
        
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";

        var children = new List<HtmlNode>();

        if (Message != null)
        {
            children.Add(HtmlNode.Text(Message));
        }
        
        children.AddRange(Children.Select(c => c.Render()));

        if (Dismissible)
        {
            children.Add(new HtmlNode
            {
                Tag = "button",
                Attributes = new Dictionary<string, string?>
                {
                    ["type"] = "button",
                    ["class"] = "btn-close",
                    ["aria-label"] = "Close"
                }
            });
        }

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = children
        };
    }
}
