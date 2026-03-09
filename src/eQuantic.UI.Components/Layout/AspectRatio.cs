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
        var padding = (1.0 / Ratio) * 100.0;

        return new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-aspect-ratio {ClassName}".Trim(),
                ["style"] = $"position:relative;width:100%;padding-bottom:{padding:F4}%"
            },
            Children = {
                new DynamicElement
                {
                    TagName = "div",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["style"] = "position:absolute;inset:0"
                    },
                    Children = { BuildChildren(context) }
                }
            }
        };
    }

    private IComponent BuildChildren(RenderContext context)
    {
        if (Children.Count == 1) return Children[0];

        var wrapper = new DynamicElement { TagName = "div" };
        foreach (var child in Children) wrapper.Children.Add(child);
        return wrapper;
    }
}
