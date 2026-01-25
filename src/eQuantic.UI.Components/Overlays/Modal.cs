using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays;

public class Modal : StatelessComponent
{
    public bool IsOpen { get; set; }
    public IComponent? Body { get; set; }
    public IComponent? Footer { get; set; }
    public new string? Title { get; set; }
    public Action? OnClose { get; set; }
    public string Width { get; set; } = "w-full max-w-lg";

    public override IComponent Build(RenderContext context)
    {
        if (!IsOpen) return new NullComponent();

        return new Box
        {
            ClassName = "fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200",
            OnClick = () => OnClose?.Invoke(), // Click outside to close
            Children = {
                new Box
                {
                    ClassName = $"bg-white dark:bg-zinc-800 rounded-xl shadow-2xl overflow-hidden {Width} animate-in zoom-in-95 duration-200",
                    OnClick = () => {}, // Stop propagation (prevent closing when clicking inside)
                    Children = {
                        // Header
                        new Row {
                            ClassName = "px-6 py-4 border-b border-gray-100 dark:border-zinc-700 items-center justify-between",
                            Children = {
                                new Heading(Title ?? "Modal", 3) { ClassName = "text-lg font-semibold text-gray-900 dark:text-white" },
                                new Button {
                                    Text = "✕",
                                    ClassName = "text-gray-400 hover:text-gray-600 dark:hover:text-gray-200",
                                    OnClick = () => OnClose?.Invoke()
                                }
                            }
                        },
                        // Body
                        new Box {
                            ClassName = "p-6",
                            Children = { Body ?? new NullComponent() }
                        },
                        // Footer
                        Footer != null ? new Box {
                            ClassName = "px-6 py-4 bg-gray-50 dark:bg-zinc-900/50 border-t border-gray-100 dark:border-zinc-700 flex justify-end gap-2",
                            Children = { Footer }
                        } : new NullComponent()
                    }
                }
            }
        };
    }
}
