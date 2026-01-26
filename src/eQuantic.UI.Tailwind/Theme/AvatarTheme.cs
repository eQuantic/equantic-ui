using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class AvatarTheme : IAvatarTheme
{
    public string Root => "relative flex h-10 w-10 shrink-0 overflow-hidden rounded-full";
    public string Image => "aspect-square h-full w-full";
    public string Fallback => "flex h-full w-full items-center justify-center rounded-full bg-gray-100 dark:bg-zinc-800";
}