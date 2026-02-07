using System;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Overlays.Drawer;

public class DrawerHeader : DrawerSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new Box
        {
            ClassName = "flex flex-col space-y-1.5 p-6 text-center sm:text-left"
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class DrawerFooter : DrawerSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new Box
        {
            ClassName = "mt-auto flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2 p-6"
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}

public class DrawerTitle : DrawerSubComponent
{
    public string Text { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        return new Heading(Text, 3)
        {
            ClassName = "text-lg font-semibold leading-none tracking-tight"
        };
    }
}

public class DrawerDescription : DrawerSubComponent
{
    public string Text { get; set; } = string.Empty;

    public override IComponent Build(RenderContext context)
    {
        return new Text(Text)
        {
            ClassName = "text-sm text-muted-foreground"
        };
    }
}

public class DrawerClose : DrawerSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var box = new Box
        {
            ClassName = "drawer-close",
            OnClick = () => Drawer?.OnClose?.Invoke()
        };
        foreach (var child in Children) box.Children.Add(child);
        return box;
    }
}
