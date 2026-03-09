using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class ScrollAreaTheme : IScrollAreaTheme
{
    public string Root => "relative overflow-hidden";
    public string Viewport => "h-full w-full rounded-[inherit]";
    public string Scrollbar => "flex touch-none select-none transition-colors";
    public string Thumb => "relative flex-1 rounded-full bg-gray-200 dark:bg-zinc-800";
    public string Corner => "bg-gray-100 dark:bg-zinc-800";
}
