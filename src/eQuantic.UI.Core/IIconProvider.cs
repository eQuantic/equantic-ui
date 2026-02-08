namespace eQuantic.UI.Core;

/// <summary>
/// Interface for icon providers that can resolve icon components by name.
/// </summary>
public interface IIconProvider
{
    /// <summary>
    /// Checks if this provider can resolve the given icon name.
    /// </summary>
    bool CanResolve(string name);

    /// <summary>
    /// Creates an icon component instance.
    /// </summary>
    IComponent? CreateIcon(string name, int size = 24, double strokeWidth = 2, string color = "currentColor", string? className = null);
}
