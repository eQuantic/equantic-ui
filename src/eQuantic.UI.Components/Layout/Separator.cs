using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// Visually separates content. Renders as a horizontal or vertical line.
/// </summary>
public class Separator : StatelessComponent
{
    /// <summary>
    /// Orientation of the separator. Default: Horizontal.
    /// </summary>
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <summary>
    /// Whether the separator is purely decorative. Default: true.
    /// When false, it is announced by screen readers.
    /// </summary>
    public bool Decorative { get; set; } = true;

    public override IComponent Build(RenderContext context)
    {
        var orientationClass = Orientation == Orientation.Vertical
            ? "eq-separator-vertical"
            : "eq-separator-horizontal";

        return new Box
        {
            As = "div",
            ClassName = $"eq-separator {orientationClass} {ClassName}".Trim(),
            Role = Decorative ? "none" : "separator",
            AriaOrientation = Decorative ? null : Orientation.ToString().ToLowerInvariant(),
            DataAttributes = new Dictionary<string, string> {
                ["orientation"] = Orientation.ToString().ToLowerInvariant()
            }
        };
    }
}
