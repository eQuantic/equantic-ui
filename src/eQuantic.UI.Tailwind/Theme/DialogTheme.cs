using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class DialogTheme : IDialogTheme
{
    public string Overlay => "fixed inset-0 z-50 bg-black/80  data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0";
    public string Content => "fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 border border-gray-200 bg-white p-6 shadow-lg duration-200 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[state=closed]:slide-out-to-left-1/2 data-[state=closed]:slide-out-to-top-[48%] data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%] sm:rounded-lg dark:border-zinc-800 dark:bg-zinc-950";
    public string Header => "flex flex-col space-y-1.5 text-center sm:text-left";
    public string Title => "text-lg font-semibold leading-none tracking-tight";
    public string Description => "text-sm text-gray-500 dark:text-gray-400";
    public string Footer => "flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2";
}