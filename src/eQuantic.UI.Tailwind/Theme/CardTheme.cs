using System.Collections.Generic;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class CardTheme : ICardTheme
{
    public string Container => "flex flex-col bg-white dark:bg-zinc-900 rounded-xl overflow-hidden border border-gray-100 dark:border-zinc-800 transition-all duration-200";
    public string Header => "w-full px-6 py-4 border-b border-gray-50 dark:border-zinc-800 bg-white/50 dark:bg-zinc-900/50 backdrop-blur-sm";
    public string Body => "w-full p-6";
    public string Footer => "w-full px-6 py-4 bg-gray-50/30 dark:bg-zinc-900/50 border-t border-gray-50 dark:border-zinc-800 flex items-center";
    public string Title => "text-lg font-semibold text-gray-900 dark:text-white leading-tight";
    public string Description => "text-sm text-gray-600 dark:text-zinc-400 mt-1";

    public Dictionary<string, string> Shadows { get; } = new Dictionary<string, string>
    {
        ["none"] = "shadow-none",
        ["small"] = "shadow-sm",
        ["medium"] = "shadow-md hover:shadow-lg",
        ["large"] = "shadow-lg hover:shadow-xl",
        ["xlarge"] = "shadow-2xl"
    };

    public string GetShadowInfo(string shadow) => Shadows.TryGetValue(shadow?.ToLower() ?? "medium", out var s) ? s : "shadow-md";

    public string GetVariant(CardVariant variant)
    {
        return variant switch
        {
            CardVariant.Outline => "bg-transparent border-2 border-gray-200 dark:border-zinc-700 shadow-none",
            CardVariant.Elevated => "bg-white dark:bg-zinc-900 shadow-lg hover:shadow-2xl",
            CardVariant.Subtle => "bg-gray-50 dark:bg-zinc-900/50 shadow-sm border-transparent",
            CardVariant.Ghost => "bg-transparent border-none shadow-none",
            CardVariant.Default => "",  // Use base styles
            _ => ""
        };
    }
}