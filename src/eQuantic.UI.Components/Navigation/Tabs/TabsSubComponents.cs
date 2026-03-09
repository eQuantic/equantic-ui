using System;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Navigation.Tabs;

public class TabsList : TabsSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var tabsTheme = theme?.Tabs;
        var listClass = tabsTheme?.List ?? "inline-flex items-center justify-center rounded-md bg-zinc-100 p-1 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400";
        
        if (Tabs?.Vertical == true) listClass += " flex-col h-auto";

        var box = new Box
        {
            As = "div",
            ClassName = $"{listClass} {ClassName}".Trim(),
            Role = "tablist"
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class TabsTrigger : TabsSubComponent
{
    public new string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public new bool Disabled { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var tabsTheme = theme?.Tabs;
        
        var triggerBase = tabsTheme?.Trigger ?? "inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-white transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-zinc-950 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-white data-[state=active]:text-zinc-950 data-[state=active]:shadow-sm dark:ring-offset-zinc-950 dark:focus-visible:ring-zinc-300 dark:data-[state=active]:bg-zinc-950 dark:data-[state=active]:text-zinc-50";
        var activeTrigger = tabsTheme?.ActiveTrigger ?? "eq-tabs-active";
        var inactiveTrigger = tabsTheme?.InactiveTrigger ?? "eq-tabs-inactive";

        var isActive = Value == ActiveValue;
        var triggerStyle = $"{triggerBase} {(isActive ? activeTrigger : inactiveTrigger)} {ClassName}".Trim();

        var trigger = new Box
        {
            As = "button",
            ClassName = triggerStyle,
            Role = "tab",
            Type = "button",
            Disabled = Disabled,
            DataAttributes = new Dictionary<string, string> { ["state"] = isActive ? "active" : "inactive" },
            OnClick = () => {
                if (!Disabled && Tabs != null)
                {
                    Tabs.OnValueChange?.Invoke(Value);
                }
            }
        };
        if (!string.IsNullOrEmpty(Text)) trigger.Children.Add(new Text(Text));
        foreach (var child in Children) trigger.Children.Add(child);
        return trigger;
    }
}

public class TabsContent : TabsSubComponent
{
    public new string Value { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var tabsTheme = theme?.Tabs;
        var contentBase = tabsTheme?.Content ?? "mt-2 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-zinc-950 focus-visible:ring-offset-2 dark:ring-offset-zinc-950 dark:focus-visible:ring-zinc-300";

        var isActive = Value == ActiveValue;
        if (!isActive) return new NullComponent();

        var box = new Box
        {
            As = "div",
            ClassName = $"{contentBase} {ClassName}".Trim(),
            Role = "tabpanel",
            DataAttributes = new Dictionary<string, string> { ["state"] = "active" }
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}
