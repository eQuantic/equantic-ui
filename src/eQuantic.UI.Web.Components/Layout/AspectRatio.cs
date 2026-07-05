using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Layout;

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
            Style = new HtmlStyle { Position = eQuantic.UI.Core.Position.Relative, Width = "100%", PaddingBottom = $"{100 / Ratio}%" },
            Children = {
                new Box
                {
                    As = "div",
                    Style = new HtmlStyle { Position = eQuantic.UI.Core.Position.Absolute, Top = "0", Right = "0", Bottom = "0", Left = "0", Width = "100%", Height = "100%" },
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
