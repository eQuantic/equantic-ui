using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in size theme using EQ spacing scale.
/// </summary>
public class SizeThemeEQ : ISizeTheme
{
    public string GetFontSize(SizeVariant size) => size switch
    {
        SizeVariant.Small => "eq-text-sm",
        SizeVariant.Medium => "eq-text-base",
        SizeVariant.Large => "eq-text-lg",
        SizeVariant.XLarge => "eq-text-xl",
        _ => "eq-text-base"
    };

    public string GetPadding(SizeVariant size) => size switch
    {
        SizeVariant.Small => "eq-p-1",
        SizeVariant.Medium => "eq-p-2",
        SizeVariant.Large => "eq-p-4",
        SizeVariant.XLarge => "eq-p-6",
        _ => "eq-p-2"
    };

    public string GetRadius(SizeVariant size) => size switch
    {
        SizeVariant.Small => "eq-rounded-sm",
        SizeVariant.Medium => "eq-rounded-md",
        SizeVariant.Large => "eq-rounded-lg",
        SizeVariant.XLarge => "eq-rounded-xl",
        _ => "eq-rounded-md"
    };
}
