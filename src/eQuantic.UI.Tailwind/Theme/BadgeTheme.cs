using System.Collections.Generic;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Tailwind.Theme;

public class BadgeTheme : IBadgeTheme
{
    private readonly IColorTheme _colors;

    public BadgeTheme(IColorTheme colors)
    {
        _colors = colors;
    }

    public string Base => "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2";
    
    public string GetVariant(Variant variant) => variant switch
    {
        Variant.Primary or Variant.Default => $"{_colors.Primary} hover:bg-blue-600/80 dark:hover:bg-blue-500/80", // Hardcoded hover
        Variant.Secondary => $"{_colors.Secondary} hover:bg-gray-100/80 dark:hover:bg-zinc-800/80",
        Variant.Destructive => $"{_colors.Destructive} hover:bg-red-500/80 dark:hover:bg-red-900/80",
        Variant.Outline => $"{_colors.Foreground} bg-transparent border-gray-200 dark:border-zinc-800", // Using Foreground for text color
        _ => $"{_colors.Primary}"
    };
}