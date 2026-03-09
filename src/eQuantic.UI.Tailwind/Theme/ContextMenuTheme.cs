using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class ContextMenuTheme : IContextMenuTheme
{
    public string Content => "z-50 min-w-[8rem] overflow-hidden rounded-md border border-zinc-200 bg-white p-1 text-zinc-950 shadow-md data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-50";
    public string Item => "relative flex cursor-default select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none focus:bg-zinc-100 focus:text-zinc-900 data-[disabled]:pointer-events-none data-[disabled]:opacity-50 dark:focus:bg-zinc-800 dark:focus:text-zinc-50";
    public string Label => "px-2 py-1.5 text-sm font-semibold text-zinc-950 dark:text-zinc-50";
    public string Separator => "-mx-1 my-1 h-px bg-zinc-200 dark:bg-zinc-800";
    public string Shortcut => "ml-auto text-xs tracking-widest text-zinc-500 dark:text-zinc-400";
}
