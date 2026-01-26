using System.Collections.Generic;

namespace eQuantic.UI.Core.Theme;

public interface IAppTheme
{
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
}

public interface ICardTheme
{
    string Container { get; }
    string Header { get; }
    string Body { get; }
    string Footer { get; }
    Dictionary<string, string> Shadows { get; }
    string GetShadowInfo(string shadow);
}

public interface IButtonTheme
{
    string Base { get; }
    Dictionary<string, string> Variants { get; }
    string GetVariant(string variant);
}

public interface IInputTheme
{
    string Base { get; }
}

public interface ICheckboxTheme
{
    string Base { get; }
    string Checked { get; }
    string Unchecked { get; }
}

public interface ITextTheme
{
    string Base { get; }
    Dictionary<string, string> Variants { get; }
    string GetVariant(string variant);
}

public interface IBadgeTheme
{
    string Base { get; }
    Dictionary<string, string> Variants { get; }
    string GetVariant(string variant);
}

public interface IAlertTheme
{
    string Base { get; }
    string Icon { get; }
    string Title { get; }
    string Description { get; }
    Dictionary<string, string> Variants { get; }
    string GetVariant(string variant);
}

public interface ISwitchTheme
{
    string Root { get; }
    string Input { get; }
    string Thumb { get; }
    string Track { get; }
}

public interface ISelectTheme
{
    string Trigger { get; }
    string Content { get; }
    string Item { get; }
    string Base { get; } // For native select
}

public interface ITableTheme
{
    string Wrapper { get; }
    string Table { get; }
    string Header { get; }
    string Row { get; }
    string HeadCell { get; }
    string Cell { get; }
}

public interface IAvatarTheme
{
    string Root { get; }
    string Image { get; }
    string Fallback { get; }
}

public interface IDialogTheme
{
    string Overlay { get; }
    string Content { get; }
    string Header { get; }
    string Title { get; }
    string Description { get; }
    string Footer { get; }
}

public interface ITabsTheme
{
    string List { get; }
    string Trigger { get; }
    string Content { get; }
    string ActiveTrigger { get; }
    string InactiveTrigger { get; }
}
