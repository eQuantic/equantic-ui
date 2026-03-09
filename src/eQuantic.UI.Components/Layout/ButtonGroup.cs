using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// Groups multiple buttons together with connected borders.
/// Children should be Button components.
/// </summary>
public class ButtonGroup : StatelessComponent
{
    /// <summary>
    /// Orientation of the button group. Default: Horizontal.
    /// </summary>
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    public override IComponent Build(RenderContext context)
    {
        var orientationClass = Orientation == Orientation.Vertical
            ? "eq-btn-group-vertical"
            : "eq-btn-group";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{orientationClass} {ClassName}".Trim(),
                ["role"] = "group"
            }
        };

        foreach (var child in Children)
        {
            element.Children.Add(child);
        }

        return element;
    }
}

/// <summary>
/// Orientation enum for layout components.
/// </summary>
public enum Orientation
{
    Horizontal,
    Vertical
}
