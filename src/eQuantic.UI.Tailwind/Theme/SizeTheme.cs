using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class SizeTheme : ISizeTheme
{
    public string GetFontSize(SizeVariant size) => size switch
    {
        SizeVariant.Small => "text-xs",
        SizeVariant.Medium => "text-sm",
        SizeVariant.Large => "text-base",
        SizeVariant.XLarge => "text-lg",
        _ => "text-sm"
    };

    public string GetPadding(SizeVariant size) => size switch
    {
        SizeVariant.Small => "px-2 py-1",
        SizeVariant.Medium => "px-4 py-2",
        SizeVariant.Large => "px-6 py-3",
        SizeVariant.XLarge => "px-8 py-4",
        _ => "px-4 py-2"
    };

    public string GetRadius(SizeVariant size) => size switch
    {
        SizeVariant.Small => "rounded-sm",
        SizeVariant.Medium => "rounded-md",
        SizeVariant.Large => "rounded-lg",
        SizeVariant.XLarge => "rounded-xl",
        _ => "rounded-md"
    };
}
