using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class CheckboxTheme : ICheckboxTheme
{
    public string Base => "peer h-4 w-4 shrink-0 rounded border border-gray-200 ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-blue-600 data-[state=checked]:text-white dark:border-zinc-700 dark:ring-offset-zinc-950 dark:data-[state=checked]:bg-blue-600 dark:data-[state=checked]:text-white transition-all";
    public string Checked => "";
    public string Unchecked => "";

    public string Root => "peer h-4 w-4 shrink-0 rounded-sm border border-primary ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground";
    public string Indicator => "flex items-center justify-center text-current";
}