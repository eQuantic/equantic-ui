using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Button component
/// </summary>
public class Button : HtmlElement
{
    /// <summary>
    /// Button type (button, submit, reset)
    /// </summary>
    public string Type { get; set; } = "button";
    
    /// <summary>
    /// Whether the button is disabled
    /// </summary>
    public bool Disabled { get; set; }
    
    /// <summary>
    /// Button text (alternative to children)
    /// </summary>
    public string? Text { get; set; }
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["type"] = Type;
        if (Disabled) attrs["disabled"] = "true";
        
        var children = Children.Any() 
            ? Children.Select(c => c.Render()).ToList()
            : Text != null 
                ? new List<HtmlNode> { HtmlNode.Text(Text) }
                : new List<HtmlNode>();
        
        return new HtmlNode
        {
            Tag = "button",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = children
        };
    }
}

/// <summary>
/// Link component
/// </summary>
public class Link : HtmlElement
{
    /// <summary>
    /// Target URL
    /// </summary>
    public string Href { get; set; } = "#";
    
    /// <summary>
    /// Target attribute (_blank, _self, etc.)
    /// </summary>
    public string? Target { get; set; }
    
    /// <summary>
    /// Link text
    /// </summary>
    public string? Text { get; set; }
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["href"] = Href;
        if (Target != null) attrs["target"] = Target;
        if (Target == "_blank") attrs["rel"] = "noopener noreferrer";
        
        var children = Children.Any() 
            ? Children.Select(c => c.Render()).ToList()
            : Text != null 
                ? new List<HtmlNode> { HtmlNode.Text(Text) }
                : new List<HtmlNode>();
        
        return new HtmlNode
        {
            Tag = "a",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = children
        };
    }
}
