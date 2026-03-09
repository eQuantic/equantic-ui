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

        var theme = context.GetService<Core.Theme.IAppTheme>();
        var backdropClass = theme?.Drawer?.Backdrop ?? "eq-drawer-backdrop";
        var panelClass = theme?.Drawer?.Panel ?? "eq-drawer-panel";

        var sideName = Drawer.Side.ToString().ToLowerInvariant();
        var sideClass = $"eq-drawer-{sideName}";
        var slideAnim = $"eq-drawer-slide-in-{sideName}";

        var panelWidth = (Drawer.Side == DrawerSide.Left || Drawer.Side == DrawerSide.Right) ? Width : "";

        var backdrop = new Box {
            ClassName = backdropClass,
            OnClick = () => Drawer.OnClose?.Invoke()
        };

        var panel = new Box {
            As = "div",
            ClassName = $"{panelClass} {sideClass} {panelWidth} {slideAnim}".Trim(),
            CustomAttributes = 
            {
                ["role"] = "dialog",
                ["aria-modal"] = "true",
                ["tabindex"] = "-1"
            }
        };
        foreach(var child in Children) panel.Children.Add(child);

        return new Box
        {
            ClassName = "fixed inset-0 z-50",
            Children = { backdrop, panel }
        };
    }
}
