using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// An interactive component that can be expanded or collapsed to show/hide content.
/// </summary>
public class Collapsible : StatelessComponent
{
    /// <summary>
    /// Whether the collapsible is initially open. Default: false.
    /// </summary>
    public bool DefaultOpen { get; set; }

    /// <summary>
    /// The trigger component that toggles open/close state.
    /// </summary>
    public IComponent? Trigger { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var id = Id ?? $"collapsible-{System.Guid.NewGuid():N}";
        var state = DefaultOpen ? "open" : "closed";

        var container = new Box
        {
            As = "div",
            ClassName = $"eq-collapsible {ClassName}".Trim(),
            Id = id,
            DataAttributes = new Dictionary<string, string> { ["state"] = state }
        };

        // Trigger button
        if (Trigger != null)
        {
            var triggerWrapper = new Box
            {
                As = "button",
                ClassName = "eq-collapsible-trigger",
                Type = "button"
            };
            triggerWrapper.Children.Add(Trigger);
            container.Children.Add(triggerWrapper);
        }

        // Content area
        var content = new Box
        {
            As = "div",
            Id = $"{id}-content",
            ClassName = "eq-collapsible-content",
            Style = DefaultOpen ? new HtmlStyle() : new HtmlStyle { Display = eQuantic.UI.Core.Display.None }
        };
        
        foreach (var child in Children)
        {
            content.Children.Add(child);
        }

        container.Children.Add(content);

        return container;
    }
}
