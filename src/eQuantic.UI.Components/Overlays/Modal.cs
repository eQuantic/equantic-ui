using System;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

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

        var theme = context.GetService<Core.Theme.IAppTheme>();
        var dialogTheme = theme?.Dialog;

        var overlayClass = dialogTheme?.Overlay ?? "eq-modal-overlay";
        var contentClass = dialogTheme?.Content ?? "eq-modal-content";
        var headerClass = dialogTheme?.Header ?? "eq-modal-header";
        var titleClass = dialogTheme?.Title ?? "eq-modal-title";
        var descriptorClass = dialogTheme?.Description ?? "eq-modal-description";
        var footerClass = dialogTheme?.Footer ?? "eq-modal-footer";

        // Combine width with content class nicely and add relative for absolute positioning of close button
        var contentStyle = $"{contentClass} {Width} relative".Trim();

        return new Box
        {
            ClassName = overlayClass,
            OnClick = () => OnClose?.Invoke(), // Click outside to close
            Children = {
                new Box
                {
                    As = "div",
                    ClassName = contentStyle,
                    Role = "dialog",
                    AriaModal = true,
                    TabIndex = -1,
                    CustomEvents = { ["click"] = new Action(() => {}) }, // Stop propagation (prevent closing when clicking inside)
                    Children = {
                        // Close button (absolutely positioned)
                        new Button {
                            Text = "✕",
                            Variant = Variant.Ghost,
                            ClassName = "absolute right-4 top-4 h-6 w-6 p-0 rounded-sm opacity-70 ring-offset-white transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-zinc-950 focus:ring-offset-2 disabled:pointer-events-none data-[state=open]:bg-zinc-100 data-[state=open]:text-zinc-500 dark:ring-offset-zinc-950 dark:focus:ring-zinc-300 dark:data-[state=open]:bg-zinc-800 dark:data-[state=open]:text-zinc-400",
                            OnClick = () => OnClose?.Invoke()
                        },
                        // Header
                        new Box {
                            ClassName = $"{headerClass} pr-9".Trim(), // pr-9 to avoid overlap with absolute close button
                            Children = {
                                new Heading(Title ?? "Modal", 3) { ClassName = titleClass }
                            }
                        },
                        // Body
                        new Box {
                            ClassName = "py-4", // Some internal padding might be needed or handled by grid
                            Children = { Body ?? new NullComponent() }
                        },
                        // Footer
                        Footer != null ? new Box {
                            ClassName = footerClass,
                            Children = { Footer }
                        } : new NullComponent()
                    }
                }
            }
        };
    }
}
