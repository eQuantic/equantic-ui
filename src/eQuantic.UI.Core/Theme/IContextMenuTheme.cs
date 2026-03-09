namespace eQuantic.UI.Core.Theme;

public interface IContextMenuTheme
{
    string Content { get; }
    string Item { get; }
    string Label { get; }
    string Separator { get; }
    string Shortcut { get; }
}
