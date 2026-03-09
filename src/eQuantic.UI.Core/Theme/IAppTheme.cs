using System.Collections.Generic;
namespace eQuantic.UI.Core.Theme;

public interface IAppTheme
{
    IColorTheme Colors { get; }
    ISizeTheme Sizes { get; }
    
    ICardTheme Card { get; }
    IButtonTheme Button { get; }
    IInputTheme Input { get; }
    ICheckboxTheme Checkbox { get; }
    ITextTheme Typography { get; }
    IBadgeTheme Badge { get; }
    IAlertTheme Alert { get; }
    ISwitchTheme Switch { get; }
    ISelectTheme Select { get; }
    ITableTheme Table { get; }
    IAvatarTheme Avatar { get; }
    IDialogTheme Dialog { get; }
    ITabsTheme Tabs { get; }
    ITooltipTheme Tooltip { get; }
    IDropdownMenuTheme DropdownMenu { get; }
    IContextMenuTheme ContextMenu { get; }
    IDrawerTheme Drawer { get; }
    IPaginationTheme Pagination { get; }
    IPopoverTheme Popover { get; }
    IScrollAreaTheme ScrollArea { get; }
    IInputOTPTheme InputOTP { get; }
}
