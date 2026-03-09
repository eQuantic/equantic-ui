using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class InputOTPTheme : IInputOTPTheme
{
    public string Container => "flex items-center gap-2 has-[:disabled]:opacity-50";
    public string Group => "flex items-center";
    public string Slot => "relative flex h-10 w-10 items-center justify-center border-y border-r border-gray-200 text-sm transition-all first:rounded-l-md first:border-l last:rounded-r-md dark:border-zinc-800";
    public string ActiveSlot => "z-10 ring-2 ring-blue-600 ring-offset-white dark:ring-offset-zinc-950";
    public string Separator => "text-gray-400 dark:text-gray-600";
    public string Caret => "pointer-events-none absolute inset-0 flex items-center justify-center";
}
