using System.Collections.Generic;

namespace eQuantic.UI.Web;

/// <summary>
/// The ESCAPE HATCH's element contract: a DOM element written by hand — its id, classes, inline
/// style, attributes and handlers, and the children it composes. Write-once components do not
/// implement it; they are <see cref="Primitives.UiComponent"/>s, realized per target. An app reaches
/// for this only for markup the vocabulary does not express, with a stated reason.
/// </summary>
public interface IComponent
{
    /// <summary>
    /// Unique identifier for the element (maps to HTML id attribute)
    /// </summary>
    string? Id { get; set; }

    /// <summary>
    /// CSS class names (maps to HTML class attribute)
    /// </summary>
    string? ClassName { get; set; }

    /// <summary>
    /// Inline styles
    /// </summary>
    HtmlStyle? Style { get; set; }

    /// <summary>
    /// Data attributes (data-*)
    /// </summary>
    Dictionary<string, string>? DataAttributes { get; set; }

    /// <summary>
    /// The elements this one composes, in document order.
    /// </summary>
    IList<IComponent> Children { get; }

    /// <summary>
    /// Add a child component
    /// </summary>
    void AddChild(IComponent child);

    /// <summary>
    /// Remove a child component
    /// </summary>
    void RemoveChild(IComponent child);

    /// <summary>
    /// Render the component to a virtual DOM node
    /// </summary>
    HtmlNode Render();
}
