using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Container component - renders as a div element
/// </summary>
public class Container : HtmlElement
{
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var node = new HtmlNode
        {
            Tag = "div",
            Attributes = BuildAttributes(),
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
        
        return node;
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
    public string Direction { get; set; } = "row";
    
    /// <summary>
    /// Justify content
    /// </summary>
    public string? Justify { get; set; }
    
    /// <summary>
    /// Align items
    /// </summary>
    public string? Align { get; set; }
    
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
        var flexStyle = new List<string> { "display: flex", $"flex-direction: {Direction}" };
        if (Justify != null) flexStyle.Add($"justify-content: {Justify}");
        if (Align != null) flexStyle.Add($"align-items: {Align}");
        if (Gap != null) flexStyle.Add($"gap: {Gap}");
        if (Wrap) flexStyle.Add("flex-wrap: wrap");
        
        var existingStyle = attrs.TryGetValue("style", out var s) ? s + "; " : "";
        attrs["style"] = existingStyle + string.Join("; ", flexStyle);
        
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
        Direction = "column";
    }
}

/// <summary>
/// Row - horizontal flex container
/// </summary>
public class Row : Flex
{
    public Row()
    {
        Direction = "row";
    }
}
