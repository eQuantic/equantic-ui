using System.Collections.Generic;

namespace eQuantic.UI.Core;

/// <summary>
/// Base interface for all UI components - Composite Pattern
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
    /// Compiled StyleClass for reusable styles
    /// </summary>
    StyleClass? StyleClass { get; set; }

    /// <summary>
    /// Multiple StyleClasses to combine
    /// </summary>
    IReadOnlyList<StyleClass>? StyleClasses { get; set; }

    /// <summary>
    /// Data attributes (data-*)
    /// </summary>
    Dictionary<string, string>? DataAttributes { get; set; }

    /// <summary>
    /// ARIA attributes for accessibility
    /// </summary>
    Dictionary<string, string>? AriaAttributes { get; set; }

    /// <summary>
    /// Child components - Composite Pattern
    /// </summary>
    /// <summary>
    /// Child components - Composite Pattern
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
