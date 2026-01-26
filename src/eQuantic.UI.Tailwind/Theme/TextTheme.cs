using System.Collections.Generic;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class TextTheme : ITextTheme
{
    public string Base => "text-gray-900 dark:text-gray-100";
    
    public Dictionary<string, string> Variants { get; } = new Dictionary<string, string>
    {
        ["p"] = "leading-7 [&:not(:first-child)]:mt-6",
        ["lead"] = "text-xl text-gray-500 dark:text-gray-400",
        ["large"] = "text-lg font-semibold",
        ["small"] = "text-sm font-medium leading-none",
        ["muted"] = "text-sm text-gray-500 dark:text-gray-400"
    };

    public Dictionary<int, string> Headings { get; } = new Dictionary<int, string>
    {
        [1] = "scroll-m-20 text-4xl font-extrabold tracking-tight lg:text-5xl",
        [2] = "scroll-m-20 border-b pb-2 text-3xl font-semibold tracking-tight first:mt-0",
        [3] = "scroll-m-20 text-2xl font-semibold tracking-tight",
        [4] = "scroll-m-20 text-xl font-semibold tracking-tight",
        [5] = "scroll-m-20 text-lg font-semibold tracking-tight",
        [6] = "scroll-m-20 text-base font-semibold tracking-tight"
    };

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "",
            Variant.Secondary => Variants["lead"],
            Variant.Ghost => Variants["muted"],
            Variant.Custom => "", // ClassName will handle it
            _ => ""
        };
    }

    public string GetHeading(int level) => Headings.TryGetValue(level, out var h) ? h : "";
}