using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class SizeTheme : ISizeTheme
{
    public string GetFontSize(Size size) => size switch
    {
        Size.Small => "text-xs",
        Size.Medium => "text-sm",
        Size.Large => "text-base",
        Size.XLarge => "text-lg",
        _ => "text-sm"
    };

    public string GetPadding(Size size) => size switch
    {
        Size.Small => "px-2 py-1",
        Size.Medium => "px-4 py-2",
        Size.Large => "px-6 py-3",
        Size.XLarge => "px-8 py-4",
        _ => "px-4 py-2"
    };

    public string GetRadius(Size size) => size switch
    {
        Size.Small => "rounded-sm",
        Size.Medium => "rounded-md",
        Size.Large => "rounded-lg",
        Size.XLarge => "rounded-xl",
        _ => "rounded-md"
    };
}
