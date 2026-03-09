using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// Displays a keyboard shortcut or key combination.
/// </summary>
public class Kbd : StatelessComponent
{
    /// <summary>
    /// The key text to display (e.g., "⌘", "Ctrl", "K").
    /// </summary>
    public string? Key { get; set; }

    public Kbd() { }
    public Kbd(string key) => Key = key;

    public override IComponent Build(RenderContext context)
    {
        var element = new DynamicElement
        {
            TagName = "kbd",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"eq-kbd {ClassName}".Trim()
            }
        };

        if (Children.Count > 0)
        {
            foreach (var child in Children) element.Children.Add(child);
        }
        else if (Key != null)
        {
            element.Children.Add(new Text(Key));
        }

        return element;
    }
}
