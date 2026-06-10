using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class InputTheme : IInputTheme
{
    public string Base => "flex w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm ring-offset-white file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-gray-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:bg-zinc-900 dark:ring-offset-zinc-950 dark:text-gray-100 dark:placeholder:text-gray-500 transition-colors";

    public string GetVariant(Variant variant) => variant switch
    {
        Variant.Success => "border-green-500 focus-visible:ring-green-500 dark:border-green-600",
        Variant.Warning => "border-yellow-500 focus-visible:ring-yellow-500 dark:border-yellow-600",
        Variant.Destructive => "border-red-500 focus-visible:ring-red-500 dark:border-red-600",
        Variant.Ghost => "border-transparent shadow-none",
        _ => ""  // Use base styles
    };

    public string GetSize(SizeVariant size) => size switch
    {
        SizeVariant.Small => "h-8 text-xs",
        SizeVariant.Medium => "h-10 text-sm",
        SizeVariant.Large => "h-12 text-base",
        _ => "h-10 text-sm"
    };
}
