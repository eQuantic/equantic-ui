namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Theme interface for ScrollArea component styling.
/// </summary>
public interface IScrollAreaTheme
{
    /// <summary>CSS classes for the scroll area root container.</summary>
    string Root { get; }

    /// <summary>CSS classes for the scrollable viewport.</summary>
    string Viewport { get; }

    /// <summary>CSS classes for the scrollbar track.</summary>
    string Scrollbar { get; }

    /// <summary>CSS classes for the scrollbar thumb (draggable handle).</summary>
    string Thumb { get; }

    /// <summary>CSS classes for the corner between horizontal and vertical scrollbars.</summary>
    string Corner { get; }
}
