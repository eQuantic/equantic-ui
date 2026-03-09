using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.Sheet;

/// <summary>
/// Sheet extends Drawer functionality with structured sub-components,
/// providing a panel that slides in from the edge of the screen.
///
/// Usage:
/// <code>
/// new Sheet {
///     IsOpen = isOpen,
///     Side = SheetSide.Right,
///     Children = {
///         new SheetContent {
///             Children = {
///                 new SheetHeader {
///                     Children = {
///                         new SheetTitle { Text = "Edit Profile" },
///                         new SheetDescription { Text = "Make changes here." }
///                     }
///                 },
///                 // ... form content ...
///                 new SheetFooter {
///                     Children = { new Button { Text = "Save" } }
///                 }
///             }
///         }
///     }
/// }
/// </code>
/// </summary>
public class Sheet : StatelessComponent
{
    /// <summary>Whether the sheet is visible. Default: false.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Side the sheet slides in from. Default: Right.</summary>
    public SheetSide Side { get; set; } = SheetSide.Right;

    /// <summary>Callback when the sheet should close.</summary>
    public Action? OnClose { get; set; }

    /// <summary>Width of the sheet when Side is Left/Right. Default: none (uses CSS).</summary>
    public string? Width { get; set; }

    public override IComponent Build(RenderContext context)
    {
        if (!IsOpen) return new NullComponent();

        foreach (var child in Children)
        {
            if (child is ISheetChild sheetChild)
            {
                sheetChild.SheetParent = this;
            }
        }

        var root = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-sheet {ClassName}".Trim(),
                ["data-state"] = "open"
            }
        };

        foreach (var child in Children) root.Children.Add(child);
        return root;
    }
}

/// <summary>Side from which the Sheet panel slides in.</summary>
public enum SheetSide { Top, Bottom, Left, Right }

/// <summary>Interface for Sheet child components.</summary>
public interface ISheetChild : IComponent
{
    Sheet? SheetParent { get; set; }
}

/// <summary>Base class for Sheet sub-components.</summary>
public abstract class SheetSubComponent : StatelessComponent, ISheetChild
{
    public Sheet? SheetParent { get; set; }
}

/// <summary>
/// The overlay + content panel of the Sheet.
/// </summary>
public class SheetContent : SheetSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var dialogTheme = theme?.Dialog;

        var overlayClass = dialogTheme?.Overlay ?? "eq-modal-overlay";

        var side = SheetParent?.Side ?? SheetSide.Right;
        var sideClass = side switch
        {
            SheetSide.Top => "eq-sheet-top",
            SheetSide.Bottom => "eq-sheet-bottom",
            SheetSide.Left => "eq-sheet-left",
            SheetSide.Right => "eq-sheet-right",
            _ => "eq-sheet-right"
        };

        // Overlay
        var overlay = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = overlayClass,
                ["data-state"] = "open"
            }
        };

        // Panel
        var panelAttrs = new Dictionary<string, string>
        {
            ["class"] = $"eq-sheet-content {sideClass}".Trim(),
            ["role"] = "dialog",
            ["aria-modal"] = "true",
            ["data-state"] = "open",
            ["tabindex"] = "-1"
        };

        if (!string.IsNullOrEmpty(SheetParent?.Width) &&
            (side == SheetSide.Left || side == SheetSide.Right))
        {
            panelAttrs["style"] = $"width:{SheetParent.Width}";
        }

        var panel = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = panelAttrs
        };

        // Close button
        panel.Children.Add(new DynamicElement
        {
            TagName = "button",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = "eq-sheet-close",
                ["type"] = "button",
                ["aria-label"] = "Close"
            },
            Children = {
                new DynamicElement
                {
                    TagName = "span",
                    CustomAttributes = new Dictionary<string, string> { ["aria-hidden"] = "true" },
                    Children = { new Text("✕") }
                }
            }
        });

        foreach (var child in Children) panel.Children.Add(child);

        var portal = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = "eq-sheet-portal"
            }
        };
        portal.Children.Add(overlay);
        portal.Children.Add(panel);

        return portal;
    }
}

/// <summary>Header container for Sheet title and description.</summary>
public class SheetHeader : SheetSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var headerClass = theme?.Dialog?.Header ?? "eq-modal-header";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> { ["class"] = headerClass }
        };
        foreach (var child in Children) element.Children.Add(child);
        return element;
    }
}

/// <summary>Title for the Sheet panel.</summary>
public class SheetTitle : SheetSubComponent
{
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var titleClass = theme?.Dialog?.Title ?? "eq-modal-title";

        var element = new DynamicElement
        {
            TagName = "h2",
            CustomAttributes = new Dictionary<string, string> { ["class"] = titleClass }
        };

        if (Children.Count > 0) foreach (var child in Children) element.Children.Add(child);
        else if (Text != null) element.Children.Add(new Text(Text));

        return element;
    }
}

/// <summary>Description text for the Sheet panel.</summary>
public class SheetDescription : SheetSubComponent
{
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var descClass = theme?.Dialog?.Description ?? "eq-modal-description";

        var element = new DynamicElement
        {
            TagName = "p",
            CustomAttributes = new Dictionary<string, string> { ["class"] = descClass }
        };

        if (Children.Count > 0) foreach (var child in Children) element.Children.Add(child);
        else if (Text != null) element.Children.Add(new Text(Text));

        return element;
    }
}

/// <summary>Footer container for Sheet action buttons.</summary>
public class SheetFooter : SheetSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var footerClass = theme?.Dialog?.Footer ?? "eq-modal-footer";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> { ["class"] = footerClass }
        };
        foreach (var child in Children) element.Children.Add(child);
        return element;
    }
}
