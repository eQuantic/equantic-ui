using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public class List : HtmlElement
{
    public bool Ordered { get; set; }
    public bool Unstyled { get; set; }
    public bool Flush { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = "list-group";
        if (Flush) classes += " list-group-flush";
        
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";

        return new HtmlNode
        {
            Tag = Ordered ? "ol" : "ul",
            Attributes = attrs,
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}

public class ListItem : HtmlElement
{
    public bool Active { get; set; }
    public bool Disabled { get; set; }
    public new Action? OnClick { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = "list-group-item";
        if (Active) classes += " active";
        if (Disabled) classes += " disabled";
        if (OnClick != null) classes += " list-group-item-action";
        
        var existingClass = attrs.GetValueOrDefault("class");
        attrs["class"] = string.IsNullOrEmpty(existingClass) ? classes : $"{existingClass} {classes}";

        return new HtmlNode
        {
            Tag = OnClick != null ? "button" : "li",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}
