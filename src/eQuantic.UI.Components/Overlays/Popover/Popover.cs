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

        var container = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-popover {ClassName}".Trim(),
                ["data-state"] = IsOpen ? "open" : "closed"
            }
        };

        // Trigger button
        if (Trigger != null)
        {
            var triggerWrapper = new DynamicElement
            {
                TagName = "button",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = "eq-popover-trigger",
                    ["type"] = "button",
                    ["aria-expanded"] = IsOpen ? "true" : "false",
                    ["aria-controls"] = $"{id}-content",
                    ["data-state"] = IsOpen ? "open" : "closed"
                }
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

        var content = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["id"] = $"{PopoverId}-content",
                ["class"] = contentClass,
                ["role"] = "dialog",
                ["data-state"] = "open",
                ["data-side"] = sideValue,
                ["tabindex"] = "-1"
            }
        };

        foreach (var child in Children) content.Children.Add(child);

        return content;
    }
}
