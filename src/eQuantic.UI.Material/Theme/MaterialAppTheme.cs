using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Application Theme
/// Implements IAppTheme with all M3 component themes
/// </summary>
public class MaterialAppTheme : IAppTheme
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

    public MaterialAppTheme()
    {
        Colors = new MaterialColorTheme();
        Sizes = new MaterialSizeTheme();

        Card = new MaterialCardTheme();
        Button = new MaterialButtonTheme();
        Input = new MaterialInputTheme();
        Checkbox = new MaterialCheckboxTheme();
        Typography = new MaterialTextTheme();
        Badge = new MaterialBadgeTheme();
        Alert = new MaterialAlertTheme();
        Switch = new MaterialSwitchTheme();
        Select = new MaterialSelectTheme();
        Table = new MaterialTableTheme();
        Avatar = new MaterialAvatarTheme();
        Dialog = new MaterialDialogTheme();
        Tabs = new MaterialTabsTheme();
        ContextMenu = new MaterialContextMenuTheme();
        Drawer = new MaterialDrawerTheme();
    }

    public IContextMenuTheme ContextMenu { get; }
    public IDrawerTheme Drawer { get; }
    public ITooltipTheme? Tooltip { get; }
    public IDropdownMenuTheme? DropdownMenu { get; }
    public IPaginationTheme? Pagination { get; }
    public IPopoverTheme? Popover { get; }
    public IScrollAreaTheme? ScrollArea { get; }
    public IInputOTPTheme? InputOTP { get; }
}
