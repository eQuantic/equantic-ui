using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Overlays;

/// <summary>
/// Position side for overlay components (Tooltip, Popover, Dropdown).
/// </summary>
public enum Side
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>
/// Alignment for overlay components.
/// </summary>
public enum Align
{
    Start,
    Center,
    End
}

/// <summary>
/// A popup that displays information related to an element when the element
/// receives keyboard focus or the mouse hovers over it.
///
/// Usage:
/// <code>
/// new Tooltip {
///     Content = "Add to library",
///     Side = Side.Top,
///     Children = { new Button { Text = "Hover me" } }
/// }
/// </code>
/// </summary>
public class Tooltip : StatelessComponent
{
    /// <summary>
    /// The tooltip text content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Rich content component for the tooltip (overrides Content text).
    /// </summary>
    public IComponent? ContentComponent { get; set; }

    /// <summary>
    /// Which side of the trigger the tooltip appears on. Default: Top.
    /// </summary>
    public Side Side { get; set; } = Side.Top;

    /// <summary>
    /// How to align the tooltip relative to the trigger. Default: Center.
    /// </summary>
    public Align Align { get; set; } = Align.Center;

    /// <summary>
    /// Delay in milliseconds before showing the tooltip. Default: 200.
    /// </summary>
    public int DelayDuration { get; set; } = 200;

    /// <summary>
    /// Whether the tooltip is open (controlled mode). Default: null (uncontrolled).
    /// </summary>
    public bool? Open { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var tooltipTheme = theme?.Tooltip;

        var contentClass = tooltipTheme?.Content ?? "eq-tooltip";
        var id = Id ?? $"tooltip-{Guid.NewGuid():N}";
        var sideValue = Side.ToString().ToLowerInvariant();

        // Wrapper (trigger)
        var wrapper = new Box
        {
            As = "div",
            ClassName = $"eq-tooltip-container {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string>
            {
                ["tooltip-id"] = id,
                ["tooltip-side"] = sideValue,
                ["tooltip-delay"] = DelayDuration.ToString()
            }
        };

        // Trigger element (the children)
        var trigger = new Box
        {
            As = "div",
            ClassName = "eq-tooltip-trigger",
            AriaDescribedBy = id,
            TabIndex = 0
        };
        foreach (var child in Children) trigger.Children.Add(child);

        // Tooltip content
        var tooltip = new Box
        {
            As = "div",
            Id = id,
            ClassName = $"{contentClass} eq-tooltip-{sideValue}".Trim(),
            Role = "tooltip",
            DataAttributes = new Dictionary<string, string>
            {
                ["side"] = sideValue,
                ["align"] = Align.ToString().ToLowerInvariant(),
                ["state"] = Open == true ? "open" : "closed"
            }
        };

        if (ContentComponent != null)
        {
            tooltip.Children.Add(ContentComponent);
        }
        else if (Content != null)
        {
            tooltip.Children.Add(new Text(Content));
        }

        wrapper.Children.Add(trigger);
        wrapper.Children.Add(tooltip);

        return wrapper;
    }
}
