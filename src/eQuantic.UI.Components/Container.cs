using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Container component - renders as a div element
/// </summary>
public class Container : HtmlElement
{
    /// <summary>
    /// If true, width is 100%. Otherwise max-width is applied based on breakpoints.
    /// </summary>
    public bool Fluid { get; set; }

    /// <summary>
    /// Optional custom max-width (e.g., "800px", "60ch").
    /// </summary>
    public string? MaxWidth { get; set; }

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        
        // Basic container styling
        var style = new List<string> { "margin-right: auto", "margin-left: auto", "padding-right: 15px", "padding-left: 15px", "width: 100%" };
        
        if (Fluid)
        {
            // Already width: 100%
        }
        else if (!string.IsNullOrEmpty(MaxWidth))
        {
            style.Add($"max-width: {MaxWidth}");
        }
        else
        {
            // Default max-width behavior
            style.Add("max-width: 1320px"); 
        }

        var existingStyle = attrs.TryGetValue("style", out var s) ? s + "; " : "";
        attrs["style"] = existingStyle + string.Join("; ", style);

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}

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
            Display = Display.Flex,
            FlexDirection = Direction,
            JustifyContent = Justify,
            AlignItems = Align,
            Gap = Gap,
            FlexWrap = Wrap ? FlexWrap.Wrap : FlexWrap.NoWrap
        };
        
        var existingStyle = attrs.TryGetValue("style", out var s) ? s + "; " : "";
        attrs["style"] = existingStyle + layoutStyle.ToCssString();
        
        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}

/// <summary>
/// Column - vertical flex container
/// </summary>
public class Column : Flex
{
    public Column()
    {
        Direction = FlexDirection.Column;
    }
}

/// <summary>
/// Row - horizontal flex container
/// </summary>
public class Row : Flex
{
    public Row()
    {
        Direction = FlexDirection.Row;
    }
}
