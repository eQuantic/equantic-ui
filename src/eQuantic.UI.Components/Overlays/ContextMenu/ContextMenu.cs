using System;
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
        var box = new DynamicElement
        {
            TagName = "div",
            ClassName = $"context-menu-trigger {ClassName}".Trim(),
            CustomAttributes = { ["data-context-menu-trigger"] = "true" }
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class ContextMenuContent : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new DynamicElement
        {
            TagName = "div",
            ClassName = $"z-50 min-w-[8rem] overflow-hidden rounded-md border border-zinc-200 bg-white p-1 text-zinc-950 shadow-md animate-in fade-in-80 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-50 {ClassName}".Trim(),
            CustomAttributes = { ["data-state"] = "closed" }
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
        var box = new DynamicElement
        {
            TagName = "div",
            ClassName = $"relative flex cursor-default select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none focus:bg-zinc-100 focus:text-zinc-900 data-[disabled]:pointer-events-none data-[disabled]:opacity-50 dark:focus:bg-zinc-800 dark:focus:text-zinc-50 {(Inset ? "pl-8" : "")} {ClassName}".Trim(),
            CustomAttributes = new Dictionary<string, string>
            {
                ["data-disabled"] = Disabled ? "true" : null
            }.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!)
        };
        foreach (var child in Children) box.Children.Add(child);
        if (!string.IsNullOrEmpty(Shortcut)) box.Children.Add(new ContextMenuShortcut { Text = Shortcut });
        return box;
    }
}

public class ContextMenuShortcut : StatelessComponent
{
    public string Text { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        return new Text(Text)
        {
            ClassName = $"ml-auto text-xs tracking-widest text-zinc-500 dark:text-zinc-400 {ClassName}".Trim()
        };
    }
}

public class ContextMenuSeparator : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        return new Box
        {
            ClassName = $"-mx-1 my-1 h-px bg-zinc-200 dark:bg-zinc-800 {ClassName}".Trim()
        };
    }
}
