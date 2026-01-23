using System;

namespace eQuantic.UI.Core.Styling;

/// <summary>
/// Color palette with semantic and scale-based colors
/// </summary>
public static class Colors
{
    #region Semantic Colors

    public static string Primary => "var(--color-primary)";
    public static string PrimaryLight => "var(--color-primary-light)";
    public static string PrimaryDark => "var(--color-primary-dark)";

    public static string Secondary => "var(--color-secondary)";
    public static string SecondaryLight => "var(--color-secondary-light)";
    public static string SecondaryDark => "var(--color-secondary-dark)";

    public static string Success => "var(--color-success)";
    public static string Warning => "var(--color-warning)";
    public static string Error => "var(--color-error)";
    public static string Info => "var(--color-info)";

    #endregion

    #region Base Colors

    public static string White => "#ffffff";
    public static string Black => "#000000";
    public static string Transparent => "transparent";

    #endregion

    #region Color Scales

    public static ColorScale Gray { get; } = new(
        "gray",
        "#f9fafb", "#f3f4f6", "#e5e7eb", "#d1d5db", "#9ca3af",
        "#6b7280", "#4b5563", "#374151", "#1f2937", "#111827"
    );

    public static ColorScale Red { get; } = new(
        "red",
        "#fef2f2", "#fee2e2", "#fecaca", "#fca5a5", "#f87171",
        "#ef4444", "#dc2626", "#b91c1c", "#991b1b", "#7f1d1d"
    );

    public static ColorScale Orange { get; } = new(
        "orange",
        "#fff7ed", "#ffedd5", "#fed7aa", "#fdba74", "#fb923c",
        "#f97316", "#ea580c", "#c2410c", "#9a3412", "#7c2d12"
    );

    public static ColorScale Yellow { get; } = new(
        "yellow",
        "#fefce8", "#fef9c3", "#fef08a", "#fde047", "#facc15",
        "#eab308", "#ca8a04", "#a16207", "#854d0e", "#713f12"
    );

    public static ColorScale Green { get; } = new(
        "green",
        "#f0fdf4", "#dcfce7", "#bbf7d0", "#86efac", "#4ade80",
        "#22c55e", "#16a34a", "#15803d", "#166534", "#14532d"
    );

    public static ColorScale Blue { get; } = new(
        "blue",
        "#eff6ff", "#dbeafe", "#bfdbfe", "#93c5fd", "#60a5fa",
        "#3b82f6", "#2563eb", "#1d4ed8", "#1e40af", "#1e3a8a"
    );

    public static ColorScale Indigo { get; } = new(
        "indigo",
        "#eef2ff", "#e0e7ff", "#c7d2fe", "#a5b4fc", "#818cf8",
        "#6366f1", "#4f46e5", "#4338ca", "#3730a3", "#312e81"
    );

    public static ColorScale Purple { get; } = new(
        "purple",
        "#faf5ff", "#f3e8ff", "#e9d5ff", "#d8b4fe", "#c084fc",
        "#a855f7", "#9333ea", "#7e22ce", "#6b21a8", "#581c87"
    );

    public static ColorScale Pink { get; } = new(
        "pink",
        "#fdf2f8", "#fce7f3", "#fbcfe8", "#f9a8d4", "#f472b6",
        "#ec4899", "#db2777", "#be185d", "#9d174d", "#831843"
    );

    #endregion
}

/// <summary>
/// Color scale with 10 shades (50-900)
/// </summary>
public class ColorScale
{
    private readonly string[] _shades;
    public string Name { get; }

    public ColorScale(string name, params string[] shades)
    {
        if (shades.Length != 10)
            throw new ArgumentException("Color scale must have exactly 10 shades");

        Name = name;
        _shades = shades;
    }

    /// <summary>
    /// Access color by shade index (50, 100, 200, ..., 900)
    /// </summary>
    public string this[int shade] => shade switch
    {
        50 => _shades[0],
        100 => _shades[1],
        200 => _shades[2],
        300 => _shades[3],
        400 => _shades[4],
        500 => _shades[5],
        600 => _shades[6],
        700 => _shades[7],
        800 => _shades[8],
        900 => _shades[9],
        _ => throw new ArgumentOutOfRangeException(nameof(shade), "Shade must be 50, 100, 200, 300, 400, 500, 600, 700, 800, or 900")
    };
}
