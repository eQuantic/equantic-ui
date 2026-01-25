using System.Collections.Generic;
using System.Linq;
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

        if (this.Fluid)
        {
            // Already width: 100%
        }
        else if (!string.IsNullOrEmpty(this.MaxWidth))
        {
            style.Add($"max-width: {this.MaxWidth}");
        }
        else
        {
            // Default max-width behavior
            style.Add("max-width: 1320px");
        }

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
