using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.ContextMenu;

public class ContextMenu : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new Box { ClassName = $"context-menu-root {ClassName}".Trim() };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class ContextMenuTrigger : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new Box
        {
            As = "div",
            ClassName = $"context-menu-trigger {ClassName}".Trim(),
            CustomAttributes = { 
                ["data-context-menu-trigger"] = "true",
                ["aria-haspopup"] = "menu",
                ["aria-expanded"] = "false"
            }
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class ContextMenuContent : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var contentClass = theme?.ContextMenu?.Content ?? "eq-context-menu-content";

        var box = new Box
        {
            As = "div",
            ClassName = $"{contentClass} {ClassName}".Trim(),
            CustomAttributes = { 
                ["data-state"] = "closed",
                ["role"] = "menu",
                ["aria-orientation"] = "vertical",
                ["tabindex"] = "-1"
            }
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class ContextMenuItem : StatelessComponent
{
    public bool Inset { get; set; }
    public bool Disabled { get; set; }
    public string? Shortcut { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var itemClass = theme?.ContextMenu?.Item ?? "eq-context-menu-item";
        var insetClass = Inset ? "pl-8" : "";

        var box = new Box
        {
            As = "div",
            ClassName = $"{itemClass} {insetClass} {ClassName}".Trim(),
            CustomAttributes = new Dictionary<string, string>
            {
                ["role"] = "menuitem",
                ["tabindex"] = "-1",
                ["data-disabled"] = Disabled ? "true" : null,
                ["aria-disabled"] = Disabled ? "true" : null
            }.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!)
        };
        foreach (var child in Children) box.Children.Add(child);
        if (!string.IsNullOrEmpty(Shortcut)) box.Children.Add(new ContextMenuShortcut { Text = Shortcut });
        return box;
    }
}

public class ContextMenuLabel : StatelessComponent
{
    public string? Text { get; set; }
    public bool Inset { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var labelClass = theme?.ContextMenu?.Label ?? "eq-context-menu-label";
        var insetClass = Inset ? "pl-8" : "";

        var element = new Box
        {
            As = "div",
            ClassName = $"{labelClass} {insetClass} {ClassName}".Trim()
        };

        if (Children.Any())
            foreach (var child in Children) element.Children.Add(child);
        else if (Text != null)
            element.Children.Add(new Text(Text));

        return element;
    }
}

public class ContextMenuShortcut : StatelessComponent
{
    public string Text { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var shortcutClass = theme?.ContextMenu?.Shortcut ?? "eq-context-menu-shortcut";

        return new Text(Text)
        {
            ClassName = $"{shortcutClass} {ClassName}".Trim()
        };
    }
}

public class ContextMenuSeparator : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var separatorClass = theme?.ContextMenu?.Separator ?? "eq-context-menu-separator";

        return new Box
        {
            As = "div",
            ClassName = $"{separatorClass} {ClassName}".Trim(),
            CustomAttributes = { ["role"] = "separator" }
        };
    }
}
