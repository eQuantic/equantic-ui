using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Navigation.NavigationMenu;

public class NavigationMenu : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var nav = new Box
        {
            As = "nav",
            ClassName = $"relative z-10 flex max-w-max flex-1 items-center justify-center {ClassName}".Trim(),
            AriaLabel = "Main"
        };
        foreach (var child in Children) nav.Children.Add(child);
        return nav;
    }
}

public class NavigationMenuList : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var ul = new Box
        {
            As = "ul",
            ClassName = $"group flex flex-1 list-none items-center justify-center space-x-1 {ClassName}".Trim()
        };
        foreach (var child in Children) ul.Children.Add(child);
        return ul;
    }
}
