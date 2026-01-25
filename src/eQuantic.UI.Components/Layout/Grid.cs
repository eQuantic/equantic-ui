using System.Collections.Generic;
using System.Linq;
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

        if (this.Columns is int cols)
        {
            style.Add($"grid-template-columns: repeat({cols}, 1fr)");
        }
        else if (this.Columns is string colStr)
        {
            style.Add($"grid-template-columns: {colStr}");
        }

        if (!string.IsNullOrEmpty(this.Gap)) style.Add($"gap: {this.Gap}");
        if (!string.IsNullOrEmpty(this.Rows)) style.Add($"grid-template-rows: {this.Rows}");
        
        // Use HtmlStyle for alignment and flow
        var layoutStyle = new HtmlStyle 
        { 
            GridAutoFlow = this.Flow,
            AlignItems = this.AlignItems,
            JustifyItems = this.JustifyItems
        };
        
        style.Add(layoutStyle.ToCssString());

        var s = attrs["style"];
        var existingStyle = s != null ? s + "; " : "";
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
