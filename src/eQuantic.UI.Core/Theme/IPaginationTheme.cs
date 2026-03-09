namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Theme interface for Pagination component styling.
/// </summary>
public interface IPaginationTheme
{
    /// <summary>CSS classes for the nav container.</summary>
    string Container { get; }

    /// <summary>CSS classes for the pagination list.</summary>
    string List { get; }

    /// <summary>CSS classes for a page number item.</summary>
    string Item { get; }

    /// <summary>CSS classes for the currently active page item.</summary>
    string Active { get; }

    /// <summary>CSS classes for disabled navigation items.</summary>
    string Disabled { get; }

    /// <summary>CSS classes for the ellipsis indicator.</summary>
    string Ellipsis { get; }
}
