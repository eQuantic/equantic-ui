using System.Collections.Generic;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class ButtonTheme : IButtonTheme
{
    private readonly IColorTheme _colors;

    public ButtonTheme(IColorTheme colors)
    {
        _colors = colors;
    }

    public string Base => "inline-flex items-center justify-center rounded-lg font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 disabled:opacity-50 disabled:pointer-events-none active:scale-[0.98] cursor-pointer ring-offset-white dark:ring-offset-zinc-950 focus-visible:ring-blue-500";

    public string GetVariant(Variant variant) => variant switch
    {
        Variant.Primary => $"{_colors.Primary} hover:bg-blue-700 shadow-sm hover:shadow dark:hover:bg-blue-500", // Hover states still hardcoded because ThemeColor only holds base.
        // Ideally ThemeColor has Hover/Active states? Or we construct hover classes separately?
        // User asked for "Semantic Colors". Replacing main bg is a good first step.
        // For full maturity, we might want ThemeColor to include Hover state or use CSS vars.
        // Given limitations, I will use _colors.Primary.Value (which is "bg-blue-600 text-white") and keep hover hardcoded or try to derive.
        // "bg-blue-600". "Hover" is hard without knowing the color scale.
        // I will keep existing hover for now but use _colors.Primary for base.
        Variant.Secondary => $"{_colors.Secondary} hover:bg-gray-50 hover:text-gray-900 dark:bg-zinc-900 dark:text-gray-100 dark:border-zinc-800 dark:hover:bg-zinc-800",
        Variant.Outline => $"{_colors.Border} bg-transparent hover:bg-gray-100 dark:text-gray-100 dark:hover:bg-zinc-800", // Using Border color
        Variant.Ghost => "hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-zinc-800 dark:hover:text-white",
        Variant.Destructive => $"{_colors.Destructive} hover:bg-red-600 shadow-sm hover:shadow",
        Variant.Link => "text-blue-600 underline-offset-4 hover:underline dark:text-blue-400",
        _ => $"{_colors.Primary} hover:bg-blue-700 shadow-sm hover:shadow"
    };

    public string GetSize(Size size) => size switch
    {
        Size.Small => "h-8 px-3 text-xs",
        Size.Medium => "h-10 px-4 py-2 text-sm",
        Size.Large => "h-12 px-6 text-base",
        Size.XLarge => "h-14 px-8 text-lg",
        _ => "h-10 px-4 py-2 text-sm"
    };
}