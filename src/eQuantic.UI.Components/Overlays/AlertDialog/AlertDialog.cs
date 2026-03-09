using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.AlertDialog;

/// <summary>
/// A modal dialog that interrupts the user with important content and expects a response.
/// Unlike a regular Dialog/Modal, an AlertDialog requires explicit user action to dismiss.
///
/// Usage:
/// <code>
/// new AlertDialog {
///     IsOpen = showConfirm,
///     Children = {
///         new AlertDialogContent {
///             Children = {
///                 new AlertDialogHeader {
///                     Children = {
///                         new AlertDialogTitle { Text = "Are you sure?" },
///                         new AlertDialogDescription { Text = "This action cannot be undone." }
///                     }
///                 },
///                 new AlertDialogFooter {
///                     Children = {
///                         new AlertDialogCancel { Text = "Cancel" },
///                         new AlertDialogAction { Text = "Continue" }
///                     }
///                 }
///             }
///         }
///     }
/// }
/// </code>
/// </summary>
public class AlertDialog : StatelessComponent
{
    /// <summary>Whether the dialog is open. Default: false.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Callback when the dialog should close.</summary>
    public Action? OnOpenChange { get; set; }

    public override IComponent Build(RenderContext context)
    {
        if (!IsOpen) return new NullComponent();

        foreach (var child in Children)
        {
            if (child is IAlertDialogChild adChild)
            {
                adChild.AlertDialogParent = this;
            }
        }

        var root = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-alert-dialog {ClassName}".Trim(),
                ["data-state"] = "open"
            }
        };

        foreach (var child in Children) root.Children.Add(child);
        return root;
    }
}

/// <summary>Interface for AlertDialog child components.</summary>
public interface IAlertDialogChild : IComponent
{
    AlertDialog? AlertDialogParent { get; set; }
}

/// <summary>Base class for AlertDialog sub-components.</summary>
public abstract class AlertDialogSubComponent : StatelessComponent, IAlertDialogChild
{
    public AlertDialog? AlertDialogParent { get; set; }
}

/// <summary>
/// The overlay and content container for the AlertDialog.
/// </summary>
public class AlertDialogContent : AlertDialogSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var dialogTheme = theme?.Dialog;

        var overlayClass = dialogTheme?.Overlay ?? "eq-modal-overlay";
        var contentClass = dialogTheme?.Content ?? "eq-modal-content";

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

        // Content
        var content = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = contentClass,
                ["role"] = "alertdialog",
                ["aria-modal"] = "true",
                ["data-state"] = "open",
                ["tabindex"] = "-1"
            }
        };

        foreach (var child in Children) content.Children.Add(child);

        var wrapper = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = "eq-alert-dialog-portal"
            }
        };
        wrapper.Children.Add(overlay);
        wrapper.Children.Add(content);

        return wrapper;
    }
}

/// <summary>Header container for AlertDialog title and description.</summary>
public class AlertDialogHeader : AlertDialogSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var headerClass = theme?.Dialog?.Header ?? "eq-modal-header";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = headerClass
            }
        };
        foreach (var child in Children) element.Children.Add(child);
        return element;
    }
}

/// <summary>Title for the AlertDialog.</summary>
public class AlertDialogTitle : AlertDialogSubComponent
{
    /// <summary>The title text.</summary>
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var titleClass = theme?.Dialog?.Title ?? "eq-modal-title";

        var element = new DynamicElement
        {
            TagName = "h2",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = titleClass
            }
        };

        if (Children.Count > 0)
            foreach (var child in Children) element.Children.Add(child);
        else if (Text != null)
            element.Children.Add(new Text(Text));

        return element;
    }
}

/// <summary>Description text for the AlertDialog.</summary>
public class AlertDialogDescription : AlertDialogSubComponent
{
    /// <summary>The description text.</summary>
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var descClass = theme?.Dialog?.Description ?? "eq-modal-description";

        var element = new DynamicElement
        {
            TagName = "p",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = descClass
            }
        };

        if (Children.Count > 0)
            foreach (var child in Children) element.Children.Add(child);
        else if (Text != null)
            element.Children.Add(new Text(Text));

        return element;
    }
}

/// <summary>Footer container for AlertDialog action buttons.</summary>
public class AlertDialogFooter : AlertDialogSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var footerClass = theme?.Dialog?.Footer ?? "eq-modal-footer";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = footerClass
            }
        };
        foreach (var child in Children) element.Children.Add(child);
        return element;
    }
}

/// <summary>
/// The confirm/action button in an AlertDialog.
/// Styled as a primary/destructive button by default.
/// </summary>
public class AlertDialogAction : AlertDialogSubComponent
{
    /// <summary>Button text.</summary>
    public string? Text { get; set; }

    /// <summary>Callback when clicked.</summary>
    public Action? OnClick { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var btnBase = theme?.Button?.Base ?? "eq-btn";

        var element = new DynamicElement
        {
            TagName = "button",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{btnBase} eq-btn-primary {ClassName}".Trim(),
                ["type"] = "button"
            }
        };

        if (Children.Count > 0)
            foreach (var child in Children) element.Children.Add(child);
        else if (Text != null)
            element.Children.Add(new Text(Text));

        return element;
    }
}

/// <summary>
/// The cancel button in an AlertDialog.
/// Styled as an outline/secondary button by default.
/// </summary>
public class AlertDialogCancel : AlertDialogSubComponent
{
    /// <summary>Button text.</summary>
    public string? Text { get; set; }

    /// <summary>Callback when clicked.</summary>
    public Action? OnClick { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var btnBase = theme?.Button?.Base ?? "eq-btn";

        var element = new DynamicElement
        {
            TagName = "button",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{btnBase} eq-btn-outline {ClassName}".Trim(),
                ["type"] = "button"
            }
        };

        if (Children.Count > 0)
            foreach (var child in Children) element.Children.Add(child);
        else if (Text != null)
            element.Children.Add(new Text(Text));

        return element;
    }
}
