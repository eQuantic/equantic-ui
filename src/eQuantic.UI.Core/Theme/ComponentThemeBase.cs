using eQuantic.UI.Core.Styling;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Base class for component themes that support both Tailwind and EQ styling systems.
/// Automatically detects which system is available and uses the appropriate classes.
/// </summary>
public abstract class ComponentThemeBase
{
    /// <summary>
    /// Gets the styling system being used.
    /// </summary>
    protected StyleSystem StyleSystem => StyleSystemDetector.PreferredSystem;

    /// <summary>
    /// Gets the base classes for this component.
    /// Automatically delegates to Tailwind or EQ implementation.
    /// </summary>
    public virtual string Base => StyleSystem == StyleSystem.Tailwind
        ? GetTailwindBase()
        : GetEQBase();

    /// <summary>
    /// Gets variant-specific classes.
    /// </summary>
    public virtual string GetVariant(Variant variant) => StyleSystem == StyleSystem.Tailwind
        ? GetTailwindVariant(variant)
        : GetEQVariant(variant);

    /// <summary>
    /// Gets size-specific classes.
    /// </summary>
    public virtual string GetSize(SizeVariant size) => StyleSystem == StyleSystem.Tailwind
        ? GetTailwindSize(size)
        : GetEQSize(size);

    // Abstract methods for Tailwind implementation (must be overridden by subclasses)
    protected abstract string GetTailwindBase();
    protected abstract string GetTailwindVariant(Variant variant);
    protected abstract string GetTailwindSize(SizeVariant size);

    // Abstract methods for EQ implementation (must be overridden by subclasses)
    protected abstract string GetEQBase();
    protected abstract string GetEQVariant(Variant variant);
    protected abstract string GetEQSize(SizeVariant size);
}
