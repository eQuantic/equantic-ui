namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Theme interface for Popover component styling.
/// </summary>
public interface IPopoverTheme
{
    /// <summary>CSS classes for the popover content container.</summary>
    string Content { get; }

    /// <summary>CSS classes for the popover arrow.</summary>
    string Arrow { get; }
}
