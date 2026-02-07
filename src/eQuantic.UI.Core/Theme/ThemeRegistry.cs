using eQuantic.UI.Core.Styling;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Centralized registry for component themes.
/// Automatically provides the appropriate theme based on the detected styling system.
/// </summary>
public static class ThemeRegistry
{
    private static readonly Dictionary<Type, Func<object>> _eqThemes = new();
    private static readonly Dictionary<Type, Func<object>> _tailwindThemes = new();

    static ThemeRegistry()
    {
        // Register built-in EQ themes
        RegisterEQTheme<IColorTheme>(() => new ColorThemeEQ());
        RegisterEQTheme<ISizeTheme>(() => new SizeThemeEQ());
        RegisterEQTheme<IButtonTheme>(() => new ButtonThemeEQ());
        RegisterEQTheme<ICardTheme>(() => new CardThemeEQ());
        RegisterEQTheme<IInputTheme>(() => new InputThemeEQ());
        RegisterEQTheme<ITextTheme>(() => new TextThemeEQ());
        RegisterEQTheme<ICheckboxTheme>(() => new CheckboxThemeEQ());
        RegisterEQTheme<IBadgeTheme>(() => new BadgeThemeEQ());
        RegisterEQTheme<IAlertTheme>(() => new AlertThemeEQ());
        RegisterEQTheme<ISwitchTheme>(() => new SwitchThemeEQ());
        RegisterEQTheme<ISelectTheme>(() => new SelectThemeEQ());
        RegisterEQTheme<ITableTheme>(() => new TableThemeEQ());
        RegisterEQTheme<IAvatarTheme>(() => new AvatarThemeEQ());
        RegisterEQTheme<IDialogTheme>(() => new DialogThemeEQ());
        RegisterEQTheme<ITabsTheme>(() => new TabsThemeEQ());
    }

    /// <summary>
    /// Registers an EQ theme for a specific interface.
    /// </summary>
    public static void RegisterEQTheme<TInterface>(Func<object> factory)
    {
        _eqThemes[typeof(TInterface)] = factory;
    }

    /// <summary>
    /// Registers a Tailwind theme for a specific interface.
    /// </summary>
    public static void RegisterTailwindTheme<TInterface>(Func<object> factory)
    {
        _tailwindThemes[typeof(TInterface)] = factory;
    }

    /// <summary>
    /// Gets the appropriate theme for the specified interface.
    /// Returns Tailwind theme if available, otherwise EQ theme.
    /// </summary>
    public static T GetTheme<T>() where T : class
    {
        var type = typeof(T);
        var styleSystem = StyleSystemDetector.PreferredSystem;

        if (styleSystem == StyleSystem.Tailwind && _tailwindThemes.TryGetValue(type, out var twFactory))
        {
            return (T)twFactory();
        }

        if (_eqThemes.TryGetValue(type, out var eqFactory))
        {
            return (T)eqFactory();
        }

        throw new InvalidOperationException($"No theme registered for {type.Name} in {styleSystem} system");
    }

    /// <summary>
    /// Checks if a theme is registered for the specified interface.
    /// </summary>
    public static bool HasTheme<T>()
    {
        var type = typeof(T);
        var styleSystem = StyleSystemDetector.PreferredSystem;

        return styleSystem == StyleSystem.Tailwind
            ? _tailwindThemes.ContainsKey(type)
            : _eqThemes.ContainsKey(type);
    }

    /// <summary>
    /// Clears all registered themes (useful for testing).
    /// </summary>
    internal static void Clear()
    {
        _eqThemes.Clear();
        _tailwindThemes.Clear();
    }
}
