using System;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Overlays;

public enum DrawerSide { Left, Right }

public class Drawer : StatelessComponent
{
    public bool IsOpen { get; set; }
    public DrawerSide Side { get; set; } = DrawerSide.Right;
    public IComponent? Content { get; set; }
    public Action? OnClose { get; set; }
    public string Width { get; set; } = "w-80";

    public override IComponent Build(RenderContext context)
    {
        if (!IsOpen) return new NullComponent();

        var sideClass = Side == DrawerSide.Right ? "right-0 border-l" : "left-0 border-r";
        var slideAnim = Side == DrawerSide.Right ? "slide-in-from-right" : "slide-in-from-left";

        return new Box
        {
            ClassName = "fixed inset-0 z-50 z-[100]", // High z-index
            Children = {
                // Backdrop
                new Box {
                    ClassName = "absolute inset-0 bg-black/20 backdrop-blur-sm animate-in fade-in duration-300",
                    OnClick = () => OnClose?.Invoke()
                },
                // Drawer Panel
                new Box {
                    ClassName = $"absolute inset-y-0 {sideClass} {Width} bg-white dark:bg-zinc-900 shadow-2xl animate-in {slideAnim} duration-300 border-gray-100 dark:border-zinc-800 flex flex-col",
                    // Removed OnClick to avoid potential interference
                    Children = {
                         // Close Button Area
                        new Row {
                            ClassName = "p-4 justify-end",
                            Children = {
                                new Button {
                                    Text = "✕",
                                    Variant = Variant.Ghost,
                                    ClassName = "h-10 w-10 p-0 rounded-full text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-zinc-800 relative z-50",
                                    OnClick = () => OnClose?.Invoke()
                                }
                            }
                        },
                        // Content
                        new Box {
                            ClassName = "flex-1 overflow-y-auto p-4",
                            Children = { Content ?? new NullComponent() }
                        }
                    }
                }
            }
        };
    }
}
