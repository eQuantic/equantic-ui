namespace eQuantic.UI.Core.Theming;

/// <summary>
/// Preset theme definitions ready to use out-of-the-box.
/// Each theme provides a complete color palette that can be applied via CSS variables.
/// </summary>
/// <remarks>
/// Usage: Apply theme colors via data attribute:
/// <code>
/// &lt;html data-theme="ocean"&gt;
/// </code>
/// Or programmatically:
/// <code>
/// document.documentElement.setAttribute('data-theme', 'ocean');
/// </code>
/// </remarks>
public static class PresetThemes
{
    /// <summary>
    /// Ocean theme - Cool blues and teals, professional and calm
    /// Primary: Blue, Accent: Cyan
    /// </summary>
    public static class Ocean
    {
        public const string Name = "ocean";

        // Light mode colors
        public const string Primary = "#0ea5e9";           // Sky blue
        public const string PrimaryHover = "#0284c7";
        public const string PrimaryActive = "#0369a1";
        public const string PrimarySubtle = "#e0f2fe";

        public const string Secondary = "#0891b2";         // Cyan
        public const string SecondaryHover = "#0e7490";

        public const string Success = "#14b8a6";          // Teal
        public const string Warning = "#f59e0b";
        public const string Error = "#ef4444";
        public const string Info = "#06b6d4";             // Cyan

        public const string Surface = "#ffffff";
        public const string SurfaceHover = "#f0f9ff";
        public const string Border = "#bae6fd";

        // Dark mode colors
        public const string PrimaryDark = "#38bdf8";
        public const string SurfaceDark = "#0c4a6e";
        public const string BorderDark = "#075985";
    }

    /// <summary>
    /// Forest theme - Earthy greens and browns, natural and organic
    /// Primary: Green, Accent: Emerald
    /// </summary>
    public static class Forest
    {
        public const string Name = "forest";

        // Light mode colors
        public const string Primary = "#10b981";           // Emerald
        public const string PrimaryHover = "#059669";
        public const string PrimaryActive = "#047857";
        public const string PrimarySubtle = "#d1fae5";

        public const string Secondary = "#6b7280";         // Gray (stone)
        public const string SecondaryHover = "#4b5563";

        public const string Success = "#22c55e";          // Green
        public const string Warning = "#f59e0b";
        public const string Error = "#ef4444";
        public const string Info = "#14b8a6";             // Teal

        public const string Surface = "#ffffff";
        public const string SurfaceHover = "#f0fdf4";
        public const string Border = "#bbf7d0";

        // Dark mode colors
        public const string PrimaryDark = "#34d399";
        public const string SurfaceDark = "#064e3b";
        public const string BorderDark = "#065f46";
    }

    /// <summary>
    /// Night theme - Deep purples and blues, modern and sophisticated
    /// Primary: Purple, Accent: Indigo
    /// </summary>
    public static class Night
    {
        public const string Name = "night";

        // Light mode colors
        public const string Primary = "#8b5cf6";           // Violet
        public const string PrimaryHover = "#7c3aed";
        public const string PrimaryActive = "#6d28d9";
        public const string PrimarySubtle = "#ede9fe";

        public const string Secondary = "#6366f1";         // Indigo
        public const string SecondaryHover = "#4f46e5";

        public const string Success = "#10b981";
        public const string Warning = "#f59e0b";
        public const string Error = "#ef4444";
        public const string Info = "#818cf8";             // Indigo light

        public const string Surface = "#ffffff";
        public const string SurfaceHover = "#f5f3ff";
        public const string Border = "#ddd6fe";

        // Dark mode colors
        public const string PrimaryDark = "#a78bfa";
        public const string SurfaceDark = "#4c1d95";
        public const string BorderDark = "#5b21b6";
    }

    /// <summary>
    /// Sunset theme - Warm oranges and reds, energetic and vibrant
    /// Primary: Orange, Accent: Red
    /// </summary>
    public static class Sunset
    {
        public const string Name = "sunset";

        // Light mode colors
        public const string Primary = "#f97316";           // Orange
        public const string PrimaryHover = "#ea580c";
        public const string PrimaryActive = "#c2410c";
        public const string PrimarySubtle = "#ffedd5";

        public const string Secondary = "#ef4444";         // Red
        public const string SecondaryHover = "#dc2626";

        public const string Success = "#10b981";
        public const string Warning = "#fbbf24";
        public const string Error = "#dc2626";
        public const string Info = "#f59e0b";             // Amber

        public const string Surface = "#ffffff";
        public const string SurfaceHover = "#fff7ed";
        public const string Border = "#fed7aa";

        // Dark mode colors
        public const string PrimaryDark = "#fb923c";
        public const string SurfaceDark = "#7c2d12";
        public const string BorderDark = "#9a3412";
    }

    /// <summary>
    /// Nord theme - Cool, muted palette inspired by Arctic landscapes
    /// Primary: Frost blue, Accent: Aurora green
    /// </summary>
    public static class Nord
    {
        public const string Name = "nord";

        // Nord palette colors
        public const string Primary = "#88c0d0";           // Frost (light blue)
        public const string PrimaryHover = "#81a1c1";
        public const string PrimaryActive = "#5e81ac";
        public const string PrimarySubtle = "#eceff4";

        public const string Secondary = "#d8dee9";         // Snow storm
        public const string SecondaryHover = "#e5e9f0";

        public const string Success = "#a3be8c";          // Aurora green
        public const string Warning = "#ebcb8b";          // Aurora yellow
        public const string Error = "#bf616a";            // Aurora red
        public const string Info = "#81a1c1";             // Frost blue

        public const string Surface = "#eceff4";
        public const string SurfaceHover = "#e5e9f0";
        public const string Border = "#d8dee9";

        // Dark mode colors (Polar Night palette)
        public const string PrimaryDark = "#88c0d0";
        public const string SurfaceDark = "#2e3440";      // Polar night
        public const string BorderDark = "#3b4252";
    }

    /// <summary>
    /// Material theme - Google Material Design inspired
    /// Primary: Material Blue, Accent: Material Pink
    /// </summary>
    public static class Material
    {
        public const string Name = "material";

        // Material Design colors
        public const string Primary = "#2196f3";           // Material blue
        public const string PrimaryHover = "#1976d2";
        public const string PrimaryActive = "#1565c0";
        public const string PrimarySubtle = "#e3f2fd";

        public const string Secondary = "#9c27b0";         // Material purple
        public const string SecondaryHover = "#7b1fa2";

        public const string Success = "#4caf50";          // Material green
        public const string Warning = "#ff9800";          // Material orange
        public const string Error = "#f44336";            // Material red
        public const string Info = "#03a9f4";             // Material light blue

        public const string Surface = "#ffffff";
        public const string SurfaceHover = "#fafafa";
        public const string Border = "#e0e0e0";

        // Dark mode colors
        public const string PrimaryDark = "#42a5f5";
        public const string SurfaceDark = "#212121";
        public const string BorderDark = "#424242";
    }

    /// <summary>
    /// Gets theme CSS as a string for dynamic injection.
    /// </summary>
    /// <param name="themeName">Theme name (ocean, forest, night, etc.)</param>
    /// <returns>CSS string with theme variables</returns>
    public static string GetThemeCSS(string themeName)
    {
        return themeName.ToLowerInvariant() switch
        {
            "ocean" => GenerateThemeCSS(Ocean.Name, Ocean.Primary, Ocean.PrimaryHover, Ocean.Secondary,
                Ocean.Success, Ocean.Warning, Ocean.Error, Ocean.Surface, Ocean.Border,
                Ocean.PrimaryDark, Ocean.SurfaceDark, Ocean.BorderDark),

            "forest" => GenerateThemeCSS(Forest.Name, Forest.Primary, Forest.PrimaryHover, Forest.Secondary,
                Forest.Success, Forest.Warning, Forest.Error, Forest.Surface, Forest.Border,
                Forest.PrimaryDark, Forest.SurfaceDark, Forest.BorderDark),

            "night" => GenerateThemeCSS(Night.Name, Night.Primary, Night.PrimaryHover, Night.Secondary,
                Night.Success, Night.Warning, Night.Error, Night.Surface, Night.Border,
                Night.PrimaryDark, Night.SurfaceDark, Night.BorderDark),

            "sunset" => GenerateThemeCSS(Sunset.Name, Sunset.Primary, Sunset.PrimaryHover, Sunset.Secondary,
                Sunset.Success, Sunset.Warning, Sunset.Error, Sunset.Surface, Sunset.Border,
                Sunset.PrimaryDark, Sunset.SurfaceDark, Sunset.BorderDark),

            "nord" => GenerateThemeCSS(Nord.Name, Nord.Primary, Nord.PrimaryHover, Nord.Secondary,
                Nord.Success, Nord.Warning, Nord.Error, Nord.Surface, Nord.Border,
                Nord.PrimaryDark, Nord.SurfaceDark, Nord.BorderDark),

            "material" => GenerateThemeCSS(Material.Name, Material.Primary, Material.PrimaryHover, Material.Secondary,
                Material.Success, Material.Warning, Material.Error, Material.Surface, Material.Border,
                Material.PrimaryDark, Material.SurfaceDark, Material.BorderDark),

            _ => string.Empty
        };
    }

    private static string GenerateThemeCSS(string name, string primary, string primaryHover, string secondary,
        string success, string warning, string error, string surface, string border,
        string primaryDark, string surfaceDark, string borderDark)
    {
        return $@"
[data-theme=""{name}""] {{
  --eq-primary: {primary};
  --eq-primary-hover: {primaryHover};
  --eq-secondary: {secondary};
  --eq-success: {success};
  --eq-warning: {warning};
  --eq-error: {error};
  --eq-info: {primary};
  --eq-surface: {surface};
  --eq-border: {border};
}}

[data-theme=""{name}""][data-mode=""dark""] {{
  --eq-primary: {primaryDark};
  --eq-surface: {surfaceDark};
  --eq-border: {borderDark};
}}";
    }
}
