using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Navigation;

public class TabItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public HtmlElement? Content { get; set; }
    public bool Disabled { get; set; }
}

public class Tabs : StatelessComponent
{
    public List<TabItem> Items { get; set; } = new();
    public string? ActiveTabId { get; set; }
    public bool Pills { get; set; } // Kept for API compatibility, but distinct style depends on theme
    public bool Vertical { get; set; }
    public Action<string>? OnTabChange { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var tabsTheme = theme?.Tabs;
        var activeId = ActiveTabId ?? Items.FirstOrDefault()?.Id;

        // Container (List)
        var listClass = tabsTheme?.List ?? "";
        if (Vertical) listClass += " flex-col h-auto";

        var triggerBase = tabsTheme?.Trigger ?? "";
        var activeTrigger = tabsTheme?.ActiveTrigger ?? "";
        var inactiveTrigger = tabsTheme?.InactiveTrigger ?? "";
        
        var contentBase = tabsTheme?.Content ?? "";

        var triggers = new List<IComponent>();
        var contents = new List<IComponent>();

        foreach (var tab in Items)
        {
            var isActive = tab.Id == activeId;
            var triggerStyle = $"{triggerBase} {(isActive ? activeTrigger : inactiveTrigger)}";
            if (tab.Disabled) triggerStyle += " opacity-50 pointer-events-none";

            var triggerAttrs = new Dictionary<string, string>
            {
                ["class"] = triggerStyle,
                ["data-tab-id"] = tab.Id,
                ["data-state"] = isActive ? "active" : "inactive",
                ["type"] = "button"
            };

            triggers.Add(new DynamicElement
            {
                TagName = "button",
                CustomAttributes = triggerAttrs,
                Children = { new Text(tab.Label) }
                // OnClick handling for client-side switching would go here if not handled by global runtime/delegate
            });

            var contentStyle = $"{contentBase} {(isActive ? "" : "hidden")}";
            var contentAttrs = new Dictionary<string, string>
            {
                ["class"] = contentStyle,
                ["id"] = $"tab-{tab.Id}",
                ["data-state"] = isActive ? "active" : "inactive"
            };

            var pane = new DynamicElement
            {
                TagName = "div",
                CustomAttributes = contentAttrs
            };
            if (tab.Content != null) pane.Children.Add(tab.Content);
            
            contents.Add(pane);
        }

        var listElement = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> 
            { 
                ["class"] = listClass,
                ["role"] = "tablist"
            }
        };
        foreach(var t in triggers) listElement.Children.Add(t);

        var container = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> { ["class"] = $"tabs-container {ClassName}".Trim() },
            Children = { listElement }
        };
        
        // Append all content panes
        foreach (var c in contents) container.Children.Add(c);

        return container;
    }
}
