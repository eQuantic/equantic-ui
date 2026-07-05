using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Feedback;

/// <summary>
/// Displays a placeholder animation while content is loading.
/// </summary>
public class Skeleton : StatelessComponent
{
    /// <summary>
    /// Width of the skeleton (CSS value, e.g., "200px", "100%"). Default: "100%".
    /// </summary>
    public string Width { get; set; } = "100%";

    /// <summary>
    /// Height of the skeleton (CSS value, e.g., "20px", "1rem"). Default: "1rem".
    /// </summary>
    public string Height { get; set; } = "1rem";

    /// <summary>
    /// If true, renders a circular skeleton (useful for avatars). Default: false.
    /// </summary>
    public bool Circle { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var circleClass = Circle ? "eq-skeleton-circle" : "";

        return new Box
        {
            As = "div",
            ClassName = $"eq-skeleton {circleClass} {ClassName}".Trim(),
            Style = Style != null ? new HtmlStyle { Width = Width, Height = Height } : new HtmlStyle { Width = Width, Height = Height },
            AriaHidden = true
        };
    }
}
