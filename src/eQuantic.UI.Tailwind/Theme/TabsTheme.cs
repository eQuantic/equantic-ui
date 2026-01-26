using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class TabsTheme : ITabsTheme
{
    public string List => "inline-flex h-10 items-center justify-center rounded-md bg-gray-100 p-1 text-gray-500 dark:bg-zinc-800 dark:text-gray-400";
    public string Trigger => "inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-white transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 dark:ring-offset-zinc-950 dark:focus-visible:ring-blue-600 cursor-pointer";
    public string Content => "mt-2 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 dark:ring-offset-zinc-950 dark:focus-visible:ring-blue-600 animate-in fade-in duration-200";
    public string ActiveTrigger => "bg-white text-gray-950 shadow-sm dark:bg-zinc-950 dark:text-gray-50";
    public string InactiveTrigger => "hover:bg-gray-200 hover:text-gray-900 dark:hover:bg-zinc-700 dark:hover:text-gray-100";
}