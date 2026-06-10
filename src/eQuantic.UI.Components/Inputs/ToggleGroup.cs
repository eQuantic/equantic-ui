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
    public new ToggleGroupType Type { get; set; } = ToggleGroupType.Single;

    /// <summary>
    /// Visual variant applied to all toggles. Default: Default.
    /// </summary>
    public Variant Variant { get; set; } = Variant.Default;

    /// <summary>
    /// Size applied to all toggles. Default: Medium.
    /// </summary>
    public new SizeVariant Size { get; set; } = SizeVariant.Medium;

    /// <summary>
    /// Orientation of the group. Default: Horizontal.
    /// </summary>
    public Layout.Orientation Orientation { get; set; } = Layout.Orientation.Horizontal;

    /// <summary>
    /// Whether the group is disabled. Default: false.
    /// </summary>
    public new bool Disabled { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var orientationClass = Orientation == Layout.Orientation.Vertical
            ? "eq-toggle-group-vertical"
            : "eq-toggle-group";

        var element = new Box
        {
            As = "div",
            ClassName = $"{orientationClass} {ClassName}".Trim(),
            Role = "group",
            DataAttributes = new Dictionary<string, string> {
                ["orientation"] = Orientation.ToString().ToLowerInvariant()
            }
        };

        if (Disabled) element.DataAttributes["disabled"] = "true";

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
