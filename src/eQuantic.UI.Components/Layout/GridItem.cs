using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// Child item for Grid container
/// </summary>
public class GridItem : HtmlElement
{
    /// <summary>
    /// Column span (1 to 12)
    /// </summary>
    public int? ColSpan { get; set; }

    /// <summary>
    /// Row span
    /// </summary>
    public int? RowSpan { get; set; }

    /// <summary>
    /// Column start line
    /// </summary>
    public int? ColStart { get; set; }

    /// <summary>
    /// Column end line
    /// </summary>
    public int? ColEnd { get; set; }

    /// <summary>
    /// Row start line
    /// </summary>
    public int? RowStart { get; set; }

    /// <summary>
    /// Row end line
    /// </summary>
    public int? RowEnd { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var style = new List<string>();

        if (ColSpan.HasValue) style.Add($"grid-column: span {ColSpan}");
        else if (ColStart.HasValue || ColEnd.HasValue) 
        {
            var start = ColStart?.ToString() ?? "auto";
            var end = ColEnd?.ToString() ?? "auto";
            style.Add($"grid-column: {start} / {end}");
        }

        if (RowSpan.HasValue) style.Add($"grid-row: span {RowSpan}");
        else if (RowStart.HasValue || RowEnd.HasValue)
        {
            var start = RowStart?.ToString() ?? "auto";
            var end = RowEnd?.ToString() ?? "auto";
            style.Add($"grid-row: {start} / {end}");
        }

        var existingStyle = attrs.TryGetValue("style", out var s) ? s + "; " : "";
        if (style.Count > 0)
        {
            attrs["style"] = existingStyle + string.Join("; ", style);
        }

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}
