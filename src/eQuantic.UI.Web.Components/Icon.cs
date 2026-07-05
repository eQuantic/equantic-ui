using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components;

/// <summary>
/// Generic Icon component that resolves icons by name using registered IIconProviders.
/// </summary>
public class Icon : StatelessComponent
{
    /// <summary>
    /// The name of the icon to render (e.g., "check", "plus").
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The size of the icon in pixels.
    /// </summary>
    public int Size { get; set; } = 24;

    /// <summary>
    /// The width of the icon's strokes.
    /// </summary>
    public double StrokeWidth { get; set; } = 2;

    /// <summary>
    /// The color of the icon.
    /// </summary>
    public string Color { get; set; } = "currentColor";

    public override IComponent Build(RenderContext context)
    {
        if (string.IsNullOrEmpty(Name))
        {
            return new NullComponent();
        }

        // Try to get icon providers
        var iconProviders = context.TryGetService<IEnumerable<IIconProvider>>();
        if (iconProviders != null)
        {
            foreach (var provider in iconProviders)
            {
                if (provider.CanResolve(Name))
                {
                    var component = provider.CreateIcon(Name, Size, StrokeWidth, Color, ClassName);
                    if (component != null)
                    {
                        return component;
                    }
                }
            }
        }

        // Fallback or placeholder if not found
        return new Box
        {
            As = "span",
            ClassName = $"icon-missing icon-{Name} {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["icon"] = Name }
        };
    }
}
