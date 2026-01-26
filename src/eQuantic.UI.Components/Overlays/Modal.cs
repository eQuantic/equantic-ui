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

        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var dialogTheme = theme?.Dialog;

        var overlayClass = dialogTheme?.Overlay ?? "";
        var contentClass = dialogTheme?.Content ?? "";
        var headerClass = dialogTheme?.Header ?? "";
        var titleClass = dialogTheme?.Title ?? "";
        var descriptorClass = dialogTheme?.Description ?? "";
        var footerClass = dialogTheme?.Footer ?? "";

        // Combine width with content class nicely
        var contentStyle = $"{contentClass} {Width}".Trim();

        return new Box
        {
            ClassName = overlayClass,
            OnClick = () => OnClose?.Invoke(), // Click outside to close
            Children = {
                new Box
                {
                    ClassName = contentStyle,
                    OnClick = () => {}, // Stop propagation (prevent closing when clicking inside)
                    Children = {
                        // Header
                        new Row {
                            ClassName = headerClass,
                            Children = {
                                new Heading(Title ?? "Modal", 3) { ClassName = titleClass },
                                new Button {
                                    Text = "✕",
                                    Variant = Variant.Ghost,
                                    ClassName = "h-6 w-6 p-0", // Close button small
                                    OnClick = () => OnClose?.Invoke()
                                }
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
