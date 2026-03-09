namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in application theme using EQ styling.
/// Provides default implementations for all component themes.
/// </summary>
public class AppThemeEQ : IAppTheme
{
    public IColorTheme Colors { get; }
    public ISizeTheme Sizes { get; }

    public ICardTheme Card { get; }
    public IButtonTheme Button { get; }
    public IInputTheme Input { get; }
    public ICheckboxTheme Checkbox { get; }
    public ITextTheme Typography { get; }
    public IBadgeTheme Badge { get; }
    public IAlertTheme Alert { get; }
    public ISwitchTheme Switch { get; }
    public ISelectTheme Select { get; }
    public ITableTheme Table { get; }
    public IAvatarTheme Avatar { get; }
    public IDialogTheme Dialog { get; }
    public ITabsTheme Tabs { get; }
    public ITooltipTheme Tooltip { get; }
    public IDropdownMenuTheme DropdownMenu { get; }
    public IContextMenuTheme ContextMenu { get; }
    public IDrawerTheme Drawer { get; }
    public IPaginationTheme Pagination { get; }
    public IPopoverTheme Popover { get; }
    public IScrollAreaTheme ScrollArea { get; }
    public IInputOTPTheme InputOTP { get; }

    public AppThemeEQ()
    {
        // Use ThemeRegistry to get themes automatically
        Colors = ThemeRegistry.GetTheme<IColorTheme>();
        Sizes = ThemeRegistry.GetTheme<ISizeTheme>();

        Button = ThemeRegistry.GetTheme<IButtonTheme>();
        Card = ThemeRegistry.GetTheme<ICardTheme>();
        Input = ThemeRegistry.GetTheme<IInputTheme>();
        Typography = ThemeRegistry.GetTheme<ITextTheme>();
        Checkbox = ThemeRegistry.GetTheme<ICheckboxTheme>();
        Badge = ThemeRegistry.GetTheme<IBadgeTheme>();
        Alert = ThemeRegistry.GetTheme<IAlertTheme>();
        Switch = ThemeRegistry.GetTheme<ISwitchTheme>();
        Select = ThemeRegistry.GetTheme<ISelectTheme>();
        Table = ThemeRegistry.GetTheme<ITableTheme>();
        Avatar = ThemeRegistry.GetTheme<IAvatarTheme>();
        Dialog = ThemeRegistry.GetTheme<IDialogTheme>();
        Tabs = ThemeRegistry.GetTheme<ITabsTheme>();
        Tooltip = ThemeRegistry.GetTheme<ITooltipTheme>();
        DropdownMenu = ThemeRegistry.GetTheme<IDropdownMenuTheme>();
        ContextMenu = ThemeRegistry.GetTheme<IContextMenuTheme>();
        Drawer = ThemeRegistry.GetTheme<IDrawerTheme>();
        Pagination = ThemeRegistry.GetTheme<IPaginationTheme>();
        Popover = ThemeRegistry.GetTheme<IPopoverTheme>();
        ScrollArea = ThemeRegistry.GetTheme<IScrollAreaTheme>();
        InputOTP = ThemeRegistry.GetTheme<IInputOTPTheme>();
    }
}
