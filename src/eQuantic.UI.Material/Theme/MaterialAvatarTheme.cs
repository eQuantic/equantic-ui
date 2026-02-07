using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Avatar theme implementation
/// </summary>
public class MaterialAvatarTheme : IAvatarTheme
{
    public string Root => "md-avatar";
    public string Image => "md-avatar__image";
    public string Fallback => "md-avatar__fallback";

    public string GetSize(Size size)
    {
        return size switch
        {
            Size.Small => "w-8 h-8",
            Size.Medium => "w-10 h-10",
            Size.Large => "w-12 h-12",
            _ => "w-10 h-10"
        };
    }
}
