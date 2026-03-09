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

        var container = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-collapsible {ClassName}".Trim(),
                ["data-state"] = state
            }
        };

        // Trigger button
        if (Trigger != null)
        {
            var triggerWrapper = new DynamicElement
            {
                TagName = "button",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = "eq-collapsible-trigger",
                    ["type"] = "button",
                    ["onclick"] = $"(function(){{var c=document.getElementById('{id}');if(c){{var s=c.dataset.state==='open'?'closed':'open';c.dataset.state=s;var ct=document.getElementById('{id}-content');if(ct)ct.style.display=s==='open'?'':'none'}}}})();"
                }
            };
            triggerWrapper.Children.Add(Trigger);
            container.Children.Add(triggerWrapper);
        }

        // Content area
        var content = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["id"] = $"{id}-content",
                ["class"] = "eq-collapsible-content",
                ["style"] = DefaultOpen ? "" : "display:none"
            }
        };

        foreach (var child in Children)
        {
            content.Children.Add(child);
        }

        container.Children.Add(content);

        // Override container id
        container.CustomAttributes["id"] = id;

        return container;
    }
}
