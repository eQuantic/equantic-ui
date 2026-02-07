using System.Collections.Generic;
using eQuantic.UI.Components;
using eQuantic.UI.Core;

namespace eQuantic.UI.Material.Components;

/// <summary>
/// Material Design 3 Theme Toggle Button
/// Provides a button to toggle between light, dark, and system theme modes
/// </summary>
public class ThemeToggle : StatelessComponent
{
    /// <summary>
    /// Show labels for theme options
    /// </summary>
    public bool ShowLabels { get; set; } = false;

    public override IComponent Build(RenderContext context)
    {
        // Simple button that toggles theme via JavaScript
        var iconButton = new DynamicElement
        {
            TagName = "button",
            CustomAttributes = new Dictionary<string, string>
            {
                ["type"] = "button",
                ["class"] = $"{M3.IconButton.Base} {ClassName}".Trim(),
                ["aria-label"] = "Toggle theme",
                ["title"] = "Toggle theme",
                ["onclick"] = "eQuantic.Material.toggleTheme()"
            },
            Children =
            {
                // Use text icons for simplicity (can be replaced with SVG components)
                new Text("🌓")
            }
        };

        return iconButton;
    }
}
