using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class PaginationTheme : IPaginationTheme
{
    public string Container => "mx-auto flex w-full justify-center";
    public string List => "flex flex-row items-center gap-1";
    public string Item => "inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 border border-gray-200 bg-white hover:bg-gray-100 hover:text-gray-900 dark:border-zinc-800 dark:bg-zinc-950 dark:hover:bg-zinc-800 dark:hover:text-gray-50 h-10 w-10 cursor-pointer";
    public string Active => "border-blue-600 bg-blue-600 text-white hover:bg-blue-700 hover:text-white dark:border-blue-600 dark:bg-blue-600";
    public string Disabled => "opacity-50 pointer-events-none";
    public string Ellipsis => "flex h-9 w-9 items-center justify-center text-gray-500 dark:text-gray-400";
}
