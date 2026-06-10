using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public class AvatarThemeEQ : IAvatarTheme
{
    public string Root => "eq-avatar-root";
    public string Image => "eq-avatar-image";
    public string Fallback => "eq-avatar-fallback";

    public string GetSize(SizeVariant size)
    {
        return size switch
        {
            SizeVariant.Small => "eq-avatar-sm",
            SizeVariant.Large => "eq-avatar-lg",
            SizeVariant.XLarge => "eq-avatar-xl",
            _ => "eq-avatar-md"
        };
    }
}
