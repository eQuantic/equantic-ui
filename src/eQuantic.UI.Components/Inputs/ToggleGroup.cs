using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// A group of toggle buttons where one or multiple can be active.
/// </summary>
public class ToggleGroup : StatelessComponent
{
    /// <summary>
    /// Selection type. Default: Single.
    /// </summary>
    public ToggleGroupType Type { get; set; } = ToggleGroupType.Single;

    /// <summary>
    /// Visual variant applied to all toggles. Default: Default.
    /// </summary>
    public Variant Variant { get; set; } = Variant.Default;

    /// <summary>
    /// Size applied to all toggles. Default: Medium.
    /// </summary>
    public Size Size { get; set; } = Size.Medium;

    /// <summary>
    /// Orientation of the group. Default: Horizontal.
    /// </summary>
    public Layout.Orientation Orientation { get; set; } = Layout.Orientation.Horizontal;

    /// <summary>
    /// Whether the group is disabled. Default: false.
    /// </summary>
    public bool Disabled { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var orientationClass = Orientation == Layout.Orientation.Vertical
            ? "eq-toggle-group-vertical"
            : "eq-toggle-group";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{orientationClass} {ClassName}".Trim(),
            ["role"] = "group",
            ["data-orientation"] = Orientation.ToString().ToLowerInvariant()
        };

        if (Disabled) attrs["data-disabled"] = "true";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = attrs
        };

        foreach (var child in Children)
        {
            element.Children.Add(child);
        }

        return element;
    }
}

/// <summary>
/// Selection mode for ToggleGroup.
/// </summary>
public enum ToggleGroupType
{
    /// <summary>Only one toggle can be active at a time.</summary>
    Single,
    /// <summary>Multiple toggles can be active simultaneously.</summary>
    Multiple
}
