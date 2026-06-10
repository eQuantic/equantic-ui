using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Input component theme interface with support for variants and sizes
/// </summary>
public interface IInputTheme
{
    /// <summary>
    /// Base input styles
    /// </summary>
    string Base { get; }

    /// <summary>
    /// Gets variant-specific styles (for error, success states, etc.)
    /// </summary>
    string GetVariant(Variant variant);

    /// <summary>
    /// Gets size-specific styles
    /// </summary>
    string GetSize(SizeVariant size);
}
