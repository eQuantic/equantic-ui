using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Flex container - div with display: flex
/// </summary>
public class Flex : HtmlElement
{
    /// <summary>
    /// Flex direction (row, column, row-reverse, column-reverse)
    /// </summary>
    public FlexDirection Direction { get; set; } = FlexDirection.Row;
    
    /// <summary>
    /// Justify content
    /// </summary>
    public JustifyContent? Justify { get; set; }
    
    /// <summary>
    /// Align items
    /// </summary>
    public AlignItem? Align { get; set; }
    
    /// <summary>
    /// Gap between children
    /// </summary>
    public string? Gap { get; set; }
    
    /// <summary>
    /// Wrap behavior
    /// </summary>
    public bool Wrap { get; set; }
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        
        // Build inline style for flex
        var layoutStyle = new HtmlStyle 
        { 
            Display = eQuantic.UI.Core.Display.Flex,
            FlexDirection = Direction,
            JustifyContent = Justify,
            AlignItems = Align,
            Gap = Gap,
            FlexWrap = Wrap ? FlexWrap.Wrap : FlexWrap.NoWrap
        };
        
        string? existingStyle = null;
        if (attrs.TryGetValue("style", out var s))
        {
            existingStyle = s?.ToString();
        }
        
        var css = layoutStyle.ToCssString();
        attrs["style"] = !string.IsNullOrEmpty(existingStyle) 
            ? $"{existingStyle}; {css}" 
            : css;
        
        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}