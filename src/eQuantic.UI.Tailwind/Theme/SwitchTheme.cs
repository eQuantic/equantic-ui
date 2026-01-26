using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class SwitchTheme : ISwitchTheme
{
    public string Root => "peer inline-flex h-[24px] w-[44px] shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-600 focus-visible:ring-offset-2 focus-visible:ring-offset-white disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:bg-blue-600 data-[state=unchecked]:bg-gray-200 dark:focus-visible:ring-blue-600 dark:focus-visible:ring-offset-zinc-950 dark:data-[state=checked]:bg-blue-600 dark:data-[state=unchecked]:bg-zinc-700";
    public string Input => ""; // Styling handled by Root
    public string Thumb => "pointer-events-none block h-5 w-5 rounded-full bg-white shadow-lg ring-0 transition-transform data-[state=checked]:translate-x-5 data-[state=unchecked]:translate-x-0 dark:bg-zinc-950";
    public string Track => "";
}