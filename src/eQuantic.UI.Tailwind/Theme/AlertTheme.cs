using System.Collections.Generic;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class AlertTheme : IAlertTheme
{
    public string Base => "relative w-full rounded-lg border p-4 [&>svg~*]:pl-7 [&>svg+div]:translate-y-[-3px] [&>svg]:absolute [&>svg]:left-4 [&>svg]:top-4 [&>svg]:text-gray-950 dark:[&>svg]:text-gray-50";
    public string Icon => "h-4 w-4";
    public string Title => "mb-1 font-medium leading-none tracking-tight";
    public string Description => "text-sm [&_p]:leading-relaxed";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["default"] = "bg-white text-gray-950 border-gray-200 dark:bg-zinc-950 dark:text-gray-50 dark:border-zinc-800",
        ["destructive"] = "border-red-500/50 text-red-500 dark:border-red-500 [&>svg]:text-red-500 dark:border-red-900/50 dark:text-red-900 dark:dark:border-red-900 dark:[&>svg]:text-red-900",
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "default", out var v) ? v : Variants["default"];
}