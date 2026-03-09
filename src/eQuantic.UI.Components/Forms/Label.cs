using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Forms;

/// <summary>
/// Renders an accessible label associated with a form control.
/// </summary>
public class Label : StatelessComponent
{
    /// <summary>
    /// The text content of the label.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// The id of the form element this label is associated with.
    /// </summary>
    public string? For { get; set; }

    public Label() { }
    public Label(string text, string? htmlFor = null)
    {
        Text = text;
        For = htmlFor;
    }

    public override IComponent Build(RenderContext context)
    {
        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"eq-label {ClassName}".Trim()
        };

        if (!string.IsNullOrEmpty(For))
        {
            attrs["for"] = For;
        }

        var element = new DynamicElement
        {
            TagName = "label",
            CustomAttributes = attrs
        };

        if (Children.Count > 0)
        {
            foreach (var child in Children) element.Children.Add(child);
        }
        else if (Text != null)
        {
            element.Children.Add(new Text(Text));
        }

        return element;
    }
}
