using System.Collections.Generic;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class BadgeTheme : IBadgeTheme
{
    public string Base => "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["default"] = "border-transparent bg-blue-600 text-white hover:bg-blue-600/80 dark:bg-blue-500 dark:text-white dark:hover:bg-blue-500/80",
        ["secondary"] = "border-transparent bg-gray-100 text-gray-900 hover:bg-gray-100/80 dark:bg-zinc-800 dark:text-gray-100 dark:hover:bg-zinc-800/80",
        ["destructive"] = "border-transparent bg-red-500 text-white hover:bg-red-500/80 dark:bg-red-900 dark:text-white dark:hover:bg-red-900/80",
        ["outline"] = "text-gray-950 dark:text-gray-50 bg-transparent border-gray-200 dark:border-zinc-800",
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "default", out var v) ? v : Variants["default"];
}