using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public class AvatarThemeEQ : IAvatarTheme
{
    public string Root => "eq-avatar-root";
    public string Image => "eq-avatar-image";
    public string Fallback => "eq-avatar-fallback";

    public string GetSize(Size size)
    {
        return size switch
        {
            Size.Small => "eq-avatar-sm",
            Size.Large => "eq-avatar-lg",
            Size.XLarge => "eq-avatar-xl",
            _ => "eq-avatar-md"
        };
    }
}
