using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class AvatarTheme : IAvatarTheme
{
    public string Root => "relative flex h-10 w-10 shrink-0 overflow-hidden rounded-full";
    public string Image => "aspect-square h-full w-full";
    public string Fallback => "flex h-full w-full items-center justify-center rounded-full bg-gray-100 dark:bg-zinc-800";
    public string GetSize(Size size) => size switch
    {
        Size.Small => "h-8 w-8",
        Size.Large => "h-14 w-14",
        Size.XLarge => "h-20 w-20",
        _ => "h-10 w-10"
    };
}
