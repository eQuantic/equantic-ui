using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

public class ColorTheme : IColorTheme
{
    public ThemeColor Primary => new("Primary", "bg-blue-600 text-white");
    public ThemeColor Secondary => new("Secondary", "bg-white text-gray-900 border border-gray-200");
    public ThemeColor Destructive => new("Destructive", "bg-red-500 text-white");
    public ThemeColor Muted => new("Muted", "text-gray-500 dark:text-gray-400");
    public ThemeColor Accent => new("Accent", "bg-gray-100 dark:bg-zinc-800");
    public ThemeColor Border => new("Border", "border-gray-200 dark:border-zinc-800");
    public ThemeColor Input => new("Input", "border-gray-200 dark:border-zinc-800");
    public ThemeColor Ring => new("Ring", "ring-blue-600 dark:ring-blue-500");
    public ThemeColor Background => new("Background", "bg-white dark:bg-zinc-950");
    public ThemeColor Foreground => new("Foreground", "text-gray-950 dark:text-gray-50");
}
