using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class InputTheme : IInputTheme
{
    public string Base => "flex w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm ring-offset-white file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-gray-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:bg-zinc-900 dark:ring-offset-zinc-950 dark:text-gray-100 dark:placeholder:text-gray-500 transition-colors";
}