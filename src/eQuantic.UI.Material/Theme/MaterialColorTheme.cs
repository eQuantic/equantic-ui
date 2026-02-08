using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Color theme implementation
/// Uses CSS custom properties via ThemeColor values
/// </summary>
public class MaterialColorTheme : IColorTheme
{
    public ThemeColor Primary { get; } = new("Primary", "bg-[var(--md-sys-color-primary)] text-[var(--md-sys-color-on-primary)]");
    public ThemeColor Secondary { get; } = new("Secondary", "bg-[var(--md-sys-color-secondary)] text-[var(--md-sys-color-on-secondary)]");
    public ThemeColor Destructive { get; } = new("Destructive", "bg-[var(--md-sys-color-error)] text-[var(--md-sys-color-on-error)]");
    public ThemeColor Muted { get; } = new("Muted", "bg-[var(--md-sys-color-surface-variant)] text-[var(--md-sys-color-on-surface-variant)]");
    public ThemeColor Accent { get; } = new("Accent", "bg-[var(--md-sys-color-tertiary)] text-[var(--md-sys-color-on-tertiary)]");
    public ThemeColor Border { get; } = new("Border", "border-[var(--md-sys-color-outline)]");
    public ThemeColor Input { get; } = new("Input", "bg-[var(--md-sys-color-surface-container-highest)]");
    public ThemeColor Ring { get; } = new("Ring", "ring-[var(--md-sys-color-primary)]");
    public ThemeColor Background { get; } = new("Background", "bg-[var(--md-sys-color-surface)]");
    public ThemeColor Foreground { get; } = new("Foreground", "text-[var(--md-sys-color-on-surface)]");
    public ThemeColor Success { get; }
    public ThemeColor Warning { get; }
    public ThemeColor Info { get; }
}
