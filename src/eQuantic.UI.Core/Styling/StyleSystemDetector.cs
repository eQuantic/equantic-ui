namespace eQuantic.UI.Core.Styling;

/// <summary>
/// Detects which styling system is available at compile-time.
/// Used by components to automatically fallback from Tailwind to EQ.
/// </summary>
public static class StyleSystemDetector
{
    private static bool? _hasTailwind;

    /// <summary>
    /// Checks if Tailwind package (eQuantic.UI.Tailwind) is referenced.
    /// This is checked once at startup and cached.
    /// </summary>
    public static bool HasTailwind
    {
        get
        {
            if (_hasTailwind.HasValue)
                return _hasTailwind.Value;

            _hasTailwind = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "eQuantic.UI.Tailwind");

            return _hasTailwind.Value;
        }
    }

    /// <summary>
    /// Returns the preferred styling system.
    /// Tailwind is preferred if available, otherwise EQ is used.
    /// </summary>
    public static StyleSystem PreferredSystem => HasTailwind ? StyleSystem.Tailwind : StyleSystem.EQ;

    /// <summary>
    /// Resets the detection cache (useful for testing).
    /// </summary>
    internal static void Reset() => _hasTailwind = null;
}

/// <summary>
/// Enum representing available styling systems.
/// </summary>
public enum StyleSystem
{
    /// <summary>
    /// Built-in eQuantic styling (always available).
    /// </summary>
    EQ,

    /// <summary>
    /// Tailwind CSS integration (requires eQuantic.UI.Tailwind package).
    /// </summary>
    Tailwind
}
