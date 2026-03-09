using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// Displays content within a desired ratio (e.g., 16/9, 4/3, 1/1).
/// Wraps children in a container that maintains the specified aspect ratio.
/// </summary>
public class AspectRatio : StatelessComponent
{
    /// <summary>
    /// The aspect ratio as width/height (e.g., 16.0/9.0 for 16:9). Default: 1.0 (square).
    /// </summary>
    public double Ratio { get; set; } = 1.0;

    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            As = "div",
            ClassName = $"eq-aspect-ratio {ClassName}".Trim(),
            Style = new HtmlStyle($"position: relative; width: 100%; padding-bottom: {100 / Ratio}%;"),
            Children = {
                new Box
                {
                    As = "div",
                    Style = new HtmlStyle("position: absolute; top: 0; right: 0; bottom: 0; left: 0; width: 100%; height: 100%;"),
                    Children = { BuildChildren(context) }
                }
            }
        };
    }

    private IComponent BuildChildren(RenderContext context)
    {
        if (Children.Count == 1) return Children[0];

        var wrapper = new Box { As = "div" };
        foreach (var child in Children) wrapper.Children.Add(child);
        return wrapper;
    }
}
