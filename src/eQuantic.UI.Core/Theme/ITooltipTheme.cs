namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Theme interface for Tooltip component styling.
/// </summary>
public interface ITooltipTheme
{
    /// <summary>CSS classes for the tooltip content container.</summary>
    string Content { get; }

    /// <summary>CSS classes for the tooltip arrow/caret.</summary>
    string Arrow { get; }
}
