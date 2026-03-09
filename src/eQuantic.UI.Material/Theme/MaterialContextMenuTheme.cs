using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

public class MaterialContextMenuTheme : IContextMenuTheme
{
    public string Content => "z-50 min-w-[8rem] overflow-hidden rounded shadow-md bg-white text-gray-900 p-2 dark:bg-gray-800 dark:text-gray-100";
    public string Item => "relative flex cursor-pointer select-none items-center rounded-sm px-3 py-2 text-sm outline-none hover:bg-gray-100 focus:bg-gray-100 data-[disabled]:pointer-events-none data-[disabled]:opacity-50 dark:hover:bg-gray-700 dark:focus:bg-gray-700";
    public string Label => "px-3 py-2 text-sm font-medium text-gray-500 dark:text-gray-400";
    public string Separator => "my-1 h-px bg-gray-200 dark:bg-gray-700";
    public string Shortcut => "ml-auto text-xs tracking-widest text-gray-500 dark:text-gray-400";
}
