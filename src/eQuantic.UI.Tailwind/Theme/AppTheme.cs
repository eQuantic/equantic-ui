using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class AppTheme : IAppTheme
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
    public IPaginationTheme Pagination { get; }
    public IPopoverTheme Popover { get; }
    public IScrollAreaTheme ScrollArea { get; }
    public IInputOTPTheme InputOTP { get; }

    public AppTheme()
    {
        Colors = new ColorTheme();
        Sizes = new SizeTheme();

        Card = new CardTheme();
        Button = new ButtonTheme(Colors);
        Input = new InputTheme();
        Checkbox = new CheckboxTheme();
        Typography = new TextTheme();
        Badge = new BadgeTheme(Colors);
        Alert = new AlertTheme(Colors);
        Switch = new SwitchTheme();
        Select = new SelectTheme();
        Table = new TableTheme();
        Avatar = new AvatarTheme();
        Dialog = new DialogTheme();
        Tabs = new TabsTheme();
        Tooltip = new TooltipTheme();
        DropdownMenu = new DropdownMenuTheme();
        Pagination = new PaginationTheme();
        Popover = new PopoverTheme();
        ScrollArea = new ScrollAreaTheme();
        InputOTP = new InputOTPTheme();
    }
}