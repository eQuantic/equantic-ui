using System.Collections.Generic;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class CardTheme : ICardTheme
{
    public string Container => "flex flex-col bg-white dark:bg-zinc-900 rounded-xl overflow-hidden border border-gray-100 dark:border-zinc-800 transition-all duration-200";
    public string Header => "w-full px-6 py-4 border-b border-gray-50 dark:border-zinc-800 bg-white/50 dark:bg-zinc-900/50 backdrop-blur-sm";
    public string Body => "w-full p-6";
    public string Footer => "w-full px-6 py-4 bg-gray-50/30 dark:bg-zinc-900/50 border-t border-gray-50 dark:border-zinc-800 flex items-center";

    public Dictionary<string, string> Shadows { get; } = new Dictionary<string, string>
    {
        ["none"] = "shadow-none",
        ["small"] = "shadow-sm",
        ["medium"] = "shadow-md hover:shadow-lg",
        ["large"] = "shadow-lg hover:shadow-xl",
        ["xlarge"] = "shadow-2xl"
    };

    public string GetShadowInfo(string shadow) => Shadows.TryGetValue(shadow?.ToLower() ?? "medium", out var s) ? s : "shadow-md";
}