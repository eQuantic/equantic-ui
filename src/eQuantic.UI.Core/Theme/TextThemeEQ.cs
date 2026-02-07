using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in text theme using EQ styling classes.
/// Provides default styling for typography components.
/// </summary>
public class TextThemeEQ : ITextTheme
{
    public string Base => "eq-text-base";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "eq-text-primary",
            Variant.Secondary => "eq-text-secondary",
            Variant.Success => "eq-text-success",
            Variant.Warning => "eq-text-warning",
            Variant.Destructive => "eq-text-error",
            Variant.Info => "eq-text-info",
            _ => ""
        };
    }

    public string GetHeading(int level)
    {
        return level switch
        {
            1 => "eq-text-4xl eq-font-bold eq-leading-tight",
            2 => "eq-text-3xl eq-font-bold eq-leading-tight",
            3 => "eq-text-2xl eq-font-bold eq-leading-snug",
            4 => "eq-text-xl eq-font-semibold eq-leading-snug",
            5 => "eq-text-lg eq-font-semibold eq-leading-normal",
            6 => "eq-text-base eq-font-semibold eq-leading-normal",
            _ => "eq-text-base"
        };
    }
}
