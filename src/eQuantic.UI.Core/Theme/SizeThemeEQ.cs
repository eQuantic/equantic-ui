using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in size theme using EQ spacing scale.
/// </summary>
public class SizeThemeEQ : ISizeTheme
{
    public string GetFontSize(Size size) => size switch
    {
        Size.Small => "eq-text-sm",
        Size.Medium => "eq-text-base",
        Size.Large => "eq-text-lg",
        Size.XLarge => "eq-text-xl",
        _ => "eq-text-base"
    };

    public string GetPadding(Size size) => size switch
    {
        Size.Small => "eq-p-1",
        Size.Medium => "eq-p-2",
        Size.Large => "eq-p-4",
        Size.XLarge => "eq-p-6",
        _ => "eq-p-2"
    };

    public string GetRadius(Size size) => size switch
    {
        Size.Small => "eq-rounded-sm",
        Size.Medium => "eq-rounded-md",
        Size.Large => "eq-rounded-lg",
        Size.XLarge => "eq-rounded-xl",
        _ => "eq-rounded-md"
    };
}
