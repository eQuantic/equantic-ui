using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// CSS Grid container
/// </summary>
public class Grid : HtmlElement
{
    /// <summary>
    /// Number of columns (integer) or template string (e.g. "1fr 2fr")
    /// </summary>
    public object Columns { get; set; } = 12;

    /// <summary>
    /// Gap between grid items
    /// </summary>
    public string? Gap { get; set; }

    /// <summary>
    /// Grid auto flow (row, column, dense, etc.)
    /// </summary>
    public GridFlow? Flow { get; set; }

    /// <summary>
    /// Template rows
    /// </summary>
    public string? Rows { get; set; }

    /// <summary>
    /// Align items (start, end, center, stretch)
    /// </summary>
    public AlignItem? AlignItems { get; set; }

    /// <summary>
    /// Justify items (start, end, center, stretch)
    /// </summary>
    public JustifyContent? JustifyItems { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var style = new List<string> { "display: grid" };

        if (Columns is int cols)
        {
            style.Add($"grid-template-columns: repeat({cols}, 1fr)");
        }
        else if (Columns is string colStr)
        {
            style.Add($"grid-template-columns: {colStr}");
        }

        if (!string.IsNullOrEmpty(Gap)) style.Add($"gap: {Gap}");
        if (!string.IsNullOrEmpty(Rows)) style.Add($"grid-template-rows: {Rows}");
        
        // Use HtmlStyle for alignment and flow
        var layoutStyle = new HtmlStyle 
        { 
            GridAutoFlow = Flow,
            AlignItems = AlignItems,
            JustifyItems = JustifyItems
        };
        
        style.Add(layoutStyle.ToCssString());

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
