namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in application theme using EQ styling.
/// Provides default implementations for all component themes.
/// </summary>
public class AppThemeEQ : IAppTheme
{
    public IColorTheme Colors { get; }
    public ISizeTheme Sizes { get; }

    public ICardTheme? Card { get; }
    public IButtonTheme Button { get; }
    public IInputTheme? Input { get; }
    public ICheckboxTheme? Checkbox { get; }
    public ITextTheme? Typography { get; }
    public IBadgeTheme? Badge { get; }
    public IAlertTheme? Alert { get; }
    public ISwitchTheme? Switch { get; }
    public ISelectTheme? Select { get; }
    public ITableTheme? Table { get; }
    public IAvatarTheme? Avatar { get; }
    public IDialogTheme? Dialog { get; }
    public ITabsTheme? Tabs { get; }

    public AppThemeEQ()
    {
        // Use ThemeRegistry to get themes automatically
        Colors = ThemeRegistry.GetTheme<IColorTheme>();
        Sizes = ThemeRegistry.GetTheme<ISizeTheme>();

        Button = ThemeRegistry.GetTheme<IButtonTheme>();
        Card = ThemeRegistry.GetTheme<ICardTheme>();
        Input = ThemeRegistry.GetTheme<IInputTheme>();

        // TODO: Add other component themes as they are implemented
        // Checkbox = ThemeRegistry.GetTheme<ICheckboxTheme>();
        // Typography = ThemeRegistry.GetTheme<ITextTheme>();
        // Badge = ThemeRegistry.GetTheme<IBadgeTheme>();
        // ...
    }
}
