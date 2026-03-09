using System;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Navigation.NavigationMenu;

public class NavigationMenuItem : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var li = new Box
        {
            As = "li",
            ClassName = $"relative {ClassName}".Trim()
        };
        foreach (var child in Children) li.Children.Add(child);
        return li;
    }
}

public class NavigationMenuTrigger : StatelessComponent
{
    public string Text { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        var trigger = new Box
        {
            As = "button",
            ClassName = $"group inline-flex h-10 w-max items-center justify-center rounded-md bg-white px-4 py-2 text-sm font-medium transition-colors hover:bg-zinc-100 hover:text-zinc-900 focus:bg-zinc-100 focus:text-zinc-900 focus:outline-none disabled:pointer-events-none disabled:opacity-50 data-[active]:bg-zinc-100/50 data-[state=open]:bg-zinc-100/50 dark:bg-zinc-950 dark:hover:bg-zinc-800 dark:hover:text-zinc-50 dark:focus:bg-zinc-800 dark:focus:text-zinc-50 dark:data-[active]:bg-zinc-800/50 dark:data-[state=open]:bg-zinc-800/50 {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["state"] = "closed" } // Runtime handles state
        };

        trigger.Children.Add(new Text(Text));
        trigger.Children.Add(new Box {
            ClassName = "relative top-[1px] ml-1 h-3 w-3 transition duration-200 group-data-[state=open]:rotate-180",
            Children = { new Text("▼") }
        });
        foreach (var child in Children) trigger.Children.Add(child);

        return trigger;
    }
}

public class NavigationMenuContent : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new Box
        {
            ClassName = $"left-0 top-full mt-2 w-full md:absolute md:w-auto data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-90 {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["state"] = "closed" }
        };
        var inner = new Box {
            ClassName = "origin-top-right overflow-hidden rounded-md border border-zinc-200 bg-white text-zinc-950 shadow-lg dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-50"
        };
        foreach (var child in Children) inner.Children.Add(child);
        box.Children.Add(inner);
        return box;
    }
}

public class NavigationMenuLink : StatelessComponent
{
    public string? Href { get; set; }
    public bool Active { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var link = new Link
        {
            Href = Href ?? "#",
            ClassName = $"group block select-none space-y-1 rounded-md p-3 leading-none no-underline outline-none transition-colors hover:bg-zinc-100 hover:text-zinc-900 focus:bg-zinc-100 focus:text-zinc-900 dark:hover:bg-zinc-800 dark:hover:text-zinc-50 dark:focus:bg-zinc-800 dark:focus:text-zinc-50 {(Active ? "bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-50" : "")} {ClassName}".Trim()
        };
        foreach (var child in Children) link.Children.Add(child);
        return link;
    }
}
