using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// A two-state button that can be toggled on or off.
/// </summary>
public class Toggle : StatelessComponent
{
    /// <summary>
    /// Whether the toggle is pressed/active. Default: false.
    /// </summary>
    public bool Pressed { get; set; }

    /// <summary>
    /// The default pressed state for uncontrolled mode.
    /// </summary>
    public bool DefaultPressed { get; set; }

    /// <summary>
    /// Visual variant. Default: Default.
    /// </summary>
    public Variant Variant { get; set; } = Variant.Default;

    /// <summary>
    /// Size of the toggle. Default: Medium.
    /// </summary>
    public new Size Size { get; set; } = Size.Medium;

    /// <summary>
    /// Whether the toggle is disabled. Default: false.
    /// </summary>
    public new bool Disabled { get; set; }

    /// <summary>
    /// Accessible label for the toggle action.
    /// </summary>
    public new string? AriaLabel { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var isPressed = Pressed || DefaultPressed;
        var sizeClass = Size switch
        {
            Size.Small => "eq-toggle-sm",
            Size.Large => "eq-toggle-lg",
            _ => ""
        };

        var variantClass = Variant switch
        {
            Variant.Outline => "eq-toggle-outline",
            _ => ""
        };

        var element = new Box
        {
            As = "button",
            Type = "button",
            ClassName = $"eq-toggle {sizeClass} {variantClass} {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["state"] = isPressed ? "on" : "off" },
            AriaPressed = isPressed ? "true" : "false",
            Disabled = Disabled,
            AriaLabel = AriaLabel
        };

        foreach (var child in Children)
        {
            element.Children.Add(child);
        }

        return element;
    }
}
