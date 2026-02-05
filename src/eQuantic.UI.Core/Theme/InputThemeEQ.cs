using eQuantic.UI.Core.Styling;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in input theme using EQ styling classes.
/// Provides consistent input styling without external dependencies.
/// </summary>
public class InputThemeEQ : IInputTheme
{
    public string Base => (string)EQ.Input.Base;

    public string GetVariant(Variant variant)
    {
        // Inputs typically use variants for states (error, success, etc.)
        return variant switch
        {
            Variant.Primary => "",  // Default
            Variant.Success => "eq-border-success",
            Variant.Warning => "eq-border-warning",
            Variant.Destructive => "eq-border-error",
            Variant.Ghost => "eq-border-none",
            _ => ""
        };
    }

    public string GetSize(Size size)
    {
        return size switch
        {
            Size.Small => (string)EQ.Input.Sm,
            Size.Medium => "",  // Default
            Size.Large => (string)EQ.Input.Lg,
            Size.XLarge => (string)EQ.Input.Lg,  // Use Large
            _ => ""
        };
    }
}
