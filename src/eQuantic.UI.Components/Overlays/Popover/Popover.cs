using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays.Popover;

/// <summary>
/// Displays rich content in a portal, triggered by a button.
///
/// Usage:
/// <code>
/// new Popover {
///     Trigger = new Button { Text = "Open", Variant = Variant.Outline },
///     Children = {
///         new PopoverContent {
///             Children = {
///                 new Heading(4) { Text = "Dimensions" },
///                 new TextInput { Placeholder = "Width" },
///                 new TextInput { Placeholder = "Height" }
///             }
///         }
///     }
/// }
/// </code>
/// </summary>
public class Popover : StatelessComponent
{
    /// <summary>
    /// The trigger element that opens the popover.
    /// </summary>
    public IComponent? Trigger { get; set; }

    /// <summary>
    /// Whether the popover is open (controlled mode).
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Side to display the popover. Default: Bottom.
    /// </summary>
    public Side Side { get; set; } = Side.Bottom;

    /// <summary>
    /// Alignment of the popover. Default: Center.
    /// </summary>
    public Align Align { get; set; } = Align.Center;

    public override IComponent Build(RenderContext context)
    {
        var id = Id ?? $"popover-{Guid.NewGuid():N}";

        // Pass context to children
        foreach (var child in Children)
        {
            if (child is IPopoverChild popoverChild)
            {
                popoverChild.PopoverId = id;
                popoverChild.PopoverSide = Side;
                popoverChild.IsPopoverOpen = IsOpen;
            }
        }

        var container = new Box
        {
            As = "div",
            ClassName = $"eq-popover {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["state"] = IsOpen ? "open" : "closed" }
        };

        // Trigger button
        if (Trigger != null)
        {
            var triggerWrapper = new Box
            {
                As = "button",
                ClassName = "eq-popover-trigger",
                Type = "button",
                AriaExpanded = IsOpen,
                AriaControls = $"{id}-content",
                DataAttributes = new Dictionary<string, string> { ["state"] = IsOpen ? "open" : "closed" }
            };
            triggerWrapper.Children.Add(Trigger);
            container.Children.Add(triggerWrapper);
        }

        // Children (expected to be PopoverContent)
        foreach (var child in Children)
        {
            container.Children.Add(child);
        }

        return container;
    }
}

/// <summary>
/// Interface for popover child components to receive parent context.
/// </summary>
public interface IPopoverChild : IComponent
{
    string? PopoverId { get; set; }
    Side PopoverSide { get; set; }
    bool IsPopoverOpen { get; set; }
}

/// <summary>
/// The content panel displayed inside a Popover.
/// </summary>
public class PopoverContent : StatelessComponent, IPopoverChild
{
    public string? PopoverId { get; set; }
    public Side PopoverSide { get; set; } = Side.Bottom;
    public bool IsPopoverOpen { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var popoverTheme = theme?.Popover;
        var contentClass = popoverTheme?.Content ?? "eq-popover-content";
        var sideValue = PopoverSide.ToString().ToLowerInvariant();

        if (!IsPopoverOpen) return new NullComponent();

        var content = new Box
        {
            As = "div",
            Id = $"{PopoverId}-content",
            ClassName = contentClass,
            Role = "dialog",
            TabIndex = -1,
            DataAttributes = new Dictionary<string, string>
            {
                ["state"] = "open",
                ["side"] = sideValue
            }
        };

        foreach (var child in Children) content.Children.Add(child);

        return content;
    }
}
