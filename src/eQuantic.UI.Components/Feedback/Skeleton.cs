using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Feedback;

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

        return new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-skeleton {circleClass} {ClassName}".Trim(),
                ["style"] = $"width:{Width};height:{Height}",
                ["aria-hidden"] = "true"
            }
        };
    }
}
