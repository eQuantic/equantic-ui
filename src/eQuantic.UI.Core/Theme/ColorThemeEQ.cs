using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in color theme using EQ CSS classes.
/// Uses semantic design tokens for consistency.
/// </summary>
public class ColorThemeEQ : IColorTheme
{
    public ThemeColor Primary => new("Primary", "eq-btn-primary");
    public ThemeColor Secondary => new("Secondary", "eq-btn-secondary");
    public ThemeColor Destructive => new("Destructive", "eq-btn-destructive");
    public ThemeColor Muted => new("Muted", "eq-text-secondary");
    public ThemeColor Accent => new("Accent", "eq-btn-info");
    public ThemeColor Border => new("Border", "eq-border");
    public ThemeColor Input => new("Input", "eq-input");
    public ThemeColor Ring => new("Ring", "eq-border-focus");
    public ThemeColor Background => new("Background", "eq-surface");
    public ThemeColor Foreground => new("Foreground", "eq-text-primary");
}
