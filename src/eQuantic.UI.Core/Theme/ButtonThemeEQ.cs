using eQuantic.UI.Core.Styling;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in button theme using EQ styling classes.
/// Works without any external CSS framework.
/// </summary>
public class ButtonThemeEQ : ComponentThemeBase, IButtonTheme
{
    protected override string GetTailwindBase()
    {
        // This should never be called since Tailwind package provides its own theme
        throw new NotSupportedException("Tailwind theme should be used when Tailwind package is available");
    }

    protected override string GetTailwindVariant(Variant variant)
    {
        throw new NotSupportedException("Tailwind theme should be used when Tailwind package is available");
    }

    protected override string GetTailwindSize(Size size)
    {
        throw new NotSupportedException("Tailwind theme should be used when Tailwind package is available");
    }

    protected override string GetEQBase()
    {
        return (string)EQ.Button.Base;
    }

    protected override string GetEQVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => (string)new EQClass("eq-btn-primary"),
            Variant.Secondary => (string)new EQClass("eq-btn-secondary"),
            Variant.Outline => (string)new EQClass("eq-btn-outline"),
            Variant.Ghost => (string)new EQClass("eq-btn-ghost"),
            Variant.Destructive => (string)new EQClass("eq-btn-destructive"),
            Variant.Link => (string)new EQClass("eq-btn-link"),
            Variant.Success => (string)new EQClass("eq-btn-success"),
            Variant.Warning => (string)new EQClass("eq-btn-warning"),
            Variant.Info => (string)new EQClass("eq-btn-info"),
            _ => (string)new EQClass("eq-btn-primary")
        };
    }

    protected override string GetEQSize(Size size)
    {
        return size switch
        {
            Size.Small => (string)EQ.Button.Sm,
            Size.Medium => "",  // Medium is default
            Size.Large => (string)EQ.Button.Lg,
            Size.XLarge => (string)EQ.Button.Lg,  // Use same as Large for now
            _ => ""
        };
    }
}
