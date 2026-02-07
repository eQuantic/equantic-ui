using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.Drawer;

public enum DrawerSide { Top, Bottom, Left, Right }

public class Drawer : StatelessComponent
{
    public bool IsOpen { get; set; }
    public Action? OnClose { get; set; }
    public DrawerSide Side { get; set; } = DrawerSide.Right;

    public override IComponent Build(RenderContext context)
    {
        foreach (var child in Children)
        {
            if (child is IDrawerComponent drawerComponent)
            {
                drawerComponent.Drawer = this;
            }
        }
        var box = new Box { ClassName = "drawer-root" };
        foreach(var child in Children) box.Children.Add(child);
        return box;
    }
}

public interface IDrawerComponent : IComponent
{
    Drawer? Drawer { get; set; }
}

public abstract class DrawerSubComponent : StatelessComponent, IDrawerComponent
{
    public Drawer? Drawer { get; set; }
}
