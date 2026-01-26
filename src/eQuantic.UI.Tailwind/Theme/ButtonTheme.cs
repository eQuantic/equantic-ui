using System.Collections.Generic;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class ButtonTheme : IButtonTheme
{
    public string Base => "inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2 disabled:opacity-50 disabled:pointer-events-none active:scale-[0.98] cursor-pointer";

    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["primary"] = "bg-blue-600 text-white hover:bg-blue-700 shadow-sm hover:shadow dark:bg-blue-600 dark:hover:bg-blue-500",
        ["secondary"] = "bg-white text-gray-900 border border-gray-200 hover:bg-gray-50 hover:text-gray-900 dark:bg-zinc-900 dark:text-gray-100 dark:border-zinc-800 dark:hover:bg-zinc-800",
        ["outline"] = "border border-gray-200 bg-transparent hover:bg-gray-100 dark:border-zinc-800 dark:text-gray-100 dark:hover:bg-zinc-800",
        ["ghost"] = "hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-zinc-800 dark:hover:text-white",
        ["destructive"] = "bg-red-500 text-white hover:bg-red-600 shadow-sm hover:shadow",
        ["text"] = "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white",
        ["link"] = "text-blue-600 underline-offset-4 hover:underline dark:text-blue-400"
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "primary", out var v) ? v : Variants["primary"];
}