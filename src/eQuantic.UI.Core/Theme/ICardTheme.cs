using System.Collections.Generic;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Card component theme interface with support for variants and shadows
/// </summary>
public interface ICardTheme
{
    /// <summary>
    /// Base container styles for the card
    /// </summary>
    string Container { get; }

    /// <summary>
    /// Header section styles
    /// </summary>
    string Header { get; }

    /// <summary>
    /// Body/content section styles
    /// </summary>
    string Body { get; }

    /// <summary>
    /// Footer section styles
    /// </summary>
    string Footer { get; }

    /// <summary>
    /// Title styles (for CardTitle compound component)
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Description styles (for CardDescription compound component)
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Available shadow levels mapped to CSS classes
    /// </summary>
    Dictionary<string, string> Shadows { get; }

    /// <summary>
    /// Gets shadow class for a given shadow level
    /// </summary>
    string GetShadowInfo(string shadow);

    /// <summary>
    /// Gets variant-specific styles (outline, filled, elevated)
    /// </summary>
    string GetVariant(CardVariant variant);
}
