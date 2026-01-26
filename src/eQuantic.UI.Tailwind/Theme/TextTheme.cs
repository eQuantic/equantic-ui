using System.Collections.Generic;
using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Tailwind.Theme;

public class TextTheme : ITextTheme
{
    public string Base => "text-gray-900 dark:text-gray-100";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["h1"] = "scroll-m-20 text-4xl font-extrabold tracking-tight lg:text-5xl",
        ["h2"] = "scroll-m-20 border-b pb-2 text-3xl font-semibold tracking-tight first:mt-0",
        ["h3"] = "scroll-m-20 text-2xl font-semibold tracking-tight",
        ["h4"] = "scroll-m-20 text-xl font-semibold tracking-tight",
        ["p"] = "leading-7 [&:not(:first-child)]:mt-6",
        ["lead"] = "text-xl text-gray-500 dark:text-gray-400",
        ["large"] = "text-lg font-semibold",
        ["small"] = "text-sm font-medium leading-none",
        ["muted"] = "text-sm text-gray-500 dark:text-gray-400"
    };

    public string GetVariant(string variant) => Variants.TryGetValue(variant?.ToLower() ?? "", out var v) ? v : "";
}