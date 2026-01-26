using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class AppTheme : IAppTheme
{
    public ICardTheme Card { get; } = new CardTheme();
    public IButtonTheme Button { get; } = new ButtonTheme();
    public IInputTheme Input { get; } = new InputTheme();
    public ICheckboxTheme Checkbox { get; } = new CheckboxTheme();
    public ITextTheme Typography { get; } = new TextTheme();
    public IBadgeTheme Badge { get; } = new BadgeTheme();
    public IAlertTheme Alert { get; } = new AlertTheme();
    public ISwitchTheme Switch { get; } = new SwitchTheme();
    public ISelectTheme Select { get; } = new SelectTheme();
    public ITableTheme Table { get; } = new TableTheme();
    public IAvatarTheme Avatar { get; } = new AvatarTheme();
    public IDialogTheme Dialog { get; } = new DialogTheme();
    public ITabsTheme Tabs { get; } = new TabsTheme();
}