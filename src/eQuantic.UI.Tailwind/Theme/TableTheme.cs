using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class TableTheme : ITableTheme
{
    public string Wrapper => "relative w-full overflow-auto";
    public string Table => "w-full caption-bottom text-sm";
    public string Header => "[&_tr]:border-b";
    public string Row => "border-b transition-colors hover:bg-gray-100/50 data-[state=selected]:bg-gray-100 dark:hover:bg-zinc-800/50 dark:data-[state=selected]:bg-zinc-800";
    public string HeadCell => "h-12 px-4 text-left align-middle font-medium text-gray-500 [&:has([role=checkbox])]:pr-0 dark:text-gray-400";
    public string Cell => "p-4 align-middle [&:has([role=checkbox])]:pr-0";
}