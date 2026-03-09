namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Theme interface for DropdownMenu component styling.
/// </summary>
public interface IDropdownMenuTheme
{
    /// <summary>CSS classes for the dropdown content container.</summary>
    string Content { get; }

    /// <summary>CSS classes for a menu item.</summary>
    string Item { get; }

    /// <summary>CSS classes for a separator line between items.</summary>
    string Separator { get; }

    /// <summary>CSS classes for a non-interactive label/heading within the menu.</summary>
    string Label { get; }

    /// <summary>CSS classes for the shortcut text displayed alongside an item.</summary>
    string Shortcut { get; }
}
