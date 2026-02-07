using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.Drawer;

public class DrawerTrigger : DrawerSubComponent
{
    public Action? OnClick { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var box = new Box
        {
            ClassName = "drawer-trigger inline-flex",
            OnClick = () => {
                OnClick?.Invoke();
                if (Drawer != null) Drawer.IsOpen = true;
            }
        };
        foreach(var child in Children) box.Children.Add(child);
        return box;
    }
}
