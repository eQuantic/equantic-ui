using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Forms;

/// <summary>
/// Input group component for combining inputs with addons (prefix/suffix text or buttons).
///
/// Usage:
/// <code>
/// new InputGroup {
///     Children = {
///         new InputAddon { Text = "$" },
///         new TextInput {
///             Type = "number",
///             Placeholder = "Amount"
///         },
///         new InputAddon { Text = ".00" }
///     }
/// }
/// </code>
/// </summary>
public class InputGroup : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var container = new Box
        {
            ClassName = $"eq-input-group {ClassName}".Trim()
        };

        foreach (var child in Children)
        {
            container.Children.Add(child);
        }

        return container;
    }
}

/// <summary>
/// Input addon component (prefix or suffix text/button).
/// </summary>
public class InputAddon : StatelessComponent
{
    /// <summary>
    /// Addon text content
    /// </summary>
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var addon = new Box
        {
            As = "span",
            ClassName = $"eq-input-addon {ClassName}".Trim()
        };

        if (!string.IsNullOrEmpty(Text))
        {
            addon.Children.Add(new Text(Text));
        }

        foreach (var child in Children)
        {
            addon.Children.Add(child);
        }

        return addon;
    }
}
