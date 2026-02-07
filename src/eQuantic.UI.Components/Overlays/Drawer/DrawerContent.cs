using System;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Overlays.Drawer;

public class DrawerContent : DrawerSubComponent
{
    public string Width { get; set; } = "w-80";

    public override IComponent Build(RenderContext context)
    {
        if (Drawer == null || !Drawer.IsOpen) return new NullComponent();

        var sideClass = Drawer.Side switch
        {
            DrawerSide.Right => "right-0 border-l inset-y-0",
            DrawerSide.Left => "left-0 border-r inset-y-0",
            DrawerSide.Top => "top-0 border-b inset-x-0 h-auto",
            DrawerSide.Bottom => "bottom-0 border-t inset-x-0 h-auto",
            _ => "right-0 border-l inset-y-0"
        };

        var slideAnim = Drawer.Side switch
        {
            DrawerSide.Right => "slide-in-from-right",
            DrawerSide.Left => "slide-in-from-left",
            DrawerSide.Top => "slide-in-from-top",
            DrawerSide.Bottom => "slide-in-from-bottom",
            _ => "slide-in-from-right"
        };

        var panelWidth = (Drawer.Side == DrawerSide.Left || Drawer.Side == DrawerSide.Right) ? Width : "";

        var backdrop = new Box {
            ClassName = "absolute inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300",
            OnClick = () => Drawer.OnClose?.Invoke()
        };

        var panel = new Box {
            ClassName = $"absolute {sideClass} {panelWidth} bg-background shadow-2xl animate-in {slideAnim} duration-300 border-border flex flex-col"
        };
        foreach(var child in Children) panel.Children.Add(child);

        return new Box
        {
            ClassName = "fixed inset-0 z-50",
            Children = { backdrop, panel }
        };
    }
}
