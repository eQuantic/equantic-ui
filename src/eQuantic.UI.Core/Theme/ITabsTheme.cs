namespace eQuantic.UI.Core.Theme;

public interface ITabsTheme
{
    string List { get; }
    string Trigger { get; }
    string Content { get; }
    string ActiveTrigger { get; }
    string InactiveTrigger { get; }
}
