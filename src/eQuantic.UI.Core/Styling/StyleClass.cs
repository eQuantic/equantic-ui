using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace eQuantic.UI.Core;

/// <summary>
/// Reusable style class that compiles to optimized CSS with generated className
/// </summary>
public class StyleClass
{
    private string? _generatedClassName;

    #region Layout

    public string? Display { get; init; }
    public string? Position { get; init; }

    #endregion

    #region Flexbox

    public string? FlexDirection { get; init; }
    public string? JustifyContent { get; init; }
    public string? AlignItems { get; init; }
    public string? Gap { get; init; }

    #endregion

    #region Sizing

    public string? Width { get; init; }
    public string? Height { get; init; }
    public string? MaxWidth { get; init; }
    public string? MinHeight { get; init; }

    #endregion

    #region Spacing

    public Spacing? Margin { get; init; }
    public Spacing? Padding { get; init; }

    #endregion

    #region Background

    public string? BackgroundColor { get; init; }
    public string? Background { get; init; }

    #endregion

    #region Border

    public string? Border { get; init; }
    public int? BorderRadius { get; init; }
    public string? BorderColor { get; init; }

    #endregion

    #region Typography

    public string? Color { get; init; }
    public string? FontSize { get; init; }
    public string? FontWeight { get; init; }
    public string? TextAlign { get; init; }

    #endregion

    #region Effects

    public string? BoxShadow { get; init; }
    public string? Cursor { get; init; }
    public string? Transition { get; init; }

    #endregion

    #region Pseudo-classes

    /// <summary>
    /// Styles applied on :hover
    /// </summary>
    public StyleClass? Hover { get; init; }

    /// <summary>
    /// Styles applied on :active
    /// </summary>
    public StyleClass? Active { get; init; }

    /// <summary>
    /// Styles applied on :focus
    /// </summary>
    public StyleClass? Focus { get; init; }

    /// <summary>
    /// Styles applied on :disabled
    /// </summary>
    public StyleClass? Disabled { get; init; }

    #endregion

    #region Responsive

    /// <summary>
    /// Media query styles
    /// </summary>
    public Dictionary<Breakpoint, StyleClass>? Media { get; init; }

    #endregion

    /// <summary>
    /// Generated className (eqx-{hash})
    /// </summary>
    public string GeneratedClassName => _generatedClassName ??= GenerateClassName();

    /// <summary>
    /// Create a new StyleClass extending this one with overrides
    /// </summary>
    public StyleClass Extend(StyleClass overrides)
    {
        return new StyleClass
        {
            Display = overrides.Display ?? Display,
            Position = overrides.Position ?? Position,
            FlexDirection = overrides.FlexDirection ?? FlexDirection,
            JustifyContent = overrides.JustifyContent ?? JustifyContent,
            AlignItems = overrides.AlignItems ?? AlignItems,
            Gap = overrides.Gap ?? Gap,
            Width = overrides.Width ?? Width,
            Height = overrides.Height ?? Height,
            MaxWidth = overrides.MaxWidth ?? MaxWidth,
            MinHeight = overrides.MinHeight ?? MinHeight,
            Margin = overrides.Margin ?? Margin,
            Padding = overrides.Padding ?? Padding,
            BackgroundColor = overrides.BackgroundColor ?? BackgroundColor,
            Background = overrides.Background ?? Background,
            Border = overrides.Border ?? Border,
            BorderRadius = overrides.BorderRadius ?? BorderRadius,
            BorderColor = overrides.BorderColor ?? BorderColor,
            Color = overrides.Color ?? Color,
            FontSize = overrides.FontSize ?? FontSize,
            FontWeight = overrides.FontWeight ?? FontWeight,
            TextAlign = overrides.TextAlign ?? TextAlign,
            BoxShadow = overrides.BoxShadow ?? BoxShadow,
            Cursor = overrides.Cursor ?? Cursor,
            Transition = overrides.Transition ?? Transition,
            Hover = overrides.Hover ?? Hover,
            Active = overrides.Active ?? Active,
            Focus = overrides.Focus ?? Focus,
            Disabled = overrides.Disabled ?? Disabled,
            Media = overrides.Media ?? Media
        };
    }

    /// <summary>
    /// Generate CSS string for this StyleClass
    /// </summary>
    public string ToCss()
    {
        var sb = new StringBuilder();
        var className = GeneratedClassName;

        // Base styles
        sb.AppendLine($".{className} {{");
        AppendCssProperties(sb);
        sb.AppendLine("}");

        // Pseudo-classes
        if (Hover != null)
        {
            sb.AppendLine($".{className}:hover {{");
            Hover.AppendCssProperties(sb);
            sb.AppendLine("}");
        }

        if (Active != null)
        {
            sb.AppendLine($".{className}:active {{");
            Active.AppendCssProperties(sb);
            sb.AppendLine("}");
        }

        if (Focus != null)
        {
            sb.AppendLine($".{className}:focus {{");
            Focus.AppendCssProperties(sb);
            sb.AppendLine("}");
        }

        if (Disabled != null)
        {
            sb.AppendLine($".{className}:disabled {{");
            Disabled.AppendCssProperties(sb);
            sb.AppendLine("}");
        }

        // Media queries
        if (Media != null)
        {
            foreach (var (breakpoint, styles) in Media)
            {
                sb.AppendLine($"@media (max-width: {breakpoint.MaxWidth}px) {{");
                sb.AppendLine($"  .{className} {{");
                styles.AppendCssProperties(sb, "    ");
                sb.AppendLine("  }");
                sb.AppendLine("}");
            }
        }

        return sb.ToString();
    }

    private void AppendCssProperties(StringBuilder sb, string indent = "  ")
    {
        if (Display != null) sb.AppendLine($"{indent}display: {Display};");
        if (Position != null) sb.AppendLine($"{indent}position: {Position};");
        if (FlexDirection != null) sb.AppendLine($"{indent}flex-direction: {FlexDirection};");
        if (JustifyContent != null) sb.AppendLine($"{indent}justify-content: {JustifyContent};");
        if (AlignItems != null) sb.AppendLine($"{indent}align-items: {AlignItems};");
        if (Gap != null) sb.AppendLine($"{indent}gap: {Gap};");
        if (Width != null) sb.AppendLine($"{indent}width: {Width};");
        if (Height != null) sb.AppendLine($"{indent}height: {Height};");
        if (MaxWidth != null) sb.AppendLine($"{indent}max-width: {MaxWidth};");
        if (MinHeight != null) sb.AppendLine($"{indent}min-height: {MinHeight};");
        if (Margin != null) sb.AppendLine($"{indent}margin: {Margin.ToCssValue()};");
        if (Padding != null) sb.AppendLine($"{indent}padding: {Padding.ToCssValue()};");
        if (BackgroundColor != null) sb.AppendLine($"{indent}background-color: {BackgroundColor};");
        if (Background != null) sb.AppendLine($"{indent}background: {Background};");
        if (Border != null) sb.AppendLine($"{indent}border: {Border};");
        if (BorderRadius != null) sb.AppendLine($"{indent}border-radius: {BorderRadius}px;");
        if (BorderColor != null) sb.AppendLine($"{indent}border-color: {BorderColor};");
        if (Color != null) sb.AppendLine($"{indent}color: {Color};");
        if (FontSize != null) sb.AppendLine($"{indent}font-size: {FontSize};");
        if (FontWeight != null) sb.AppendLine($"{indent}font-weight: {FontWeight};");
        if (TextAlign != null) sb.AppendLine($"{indent}text-align: {TextAlign};");
        if (BoxShadow != null) sb.AppendLine($"{indent}box-shadow: {BoxShadow};");
        if (Cursor != null) sb.AppendLine($"{indent}cursor: {Cursor};");
        if (Transition != null) sb.AppendLine($"{indent}transition: {Transition};");
    }

    private string GenerateClassName()
    {
        var hash = ComputeHash(ToCss());
        return $"eqx-{hash[..6]}";
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Spacing value for margin/padding
/// </summary>
public class Spacing
{
    public int? Top { get; init; }
    public int? Right { get; init; }
    public int? Bottom { get; init; }
    public int? Left { get; init; }

    /// <summary>
    /// All sides equal
    /// </summary>
    public static Spacing All(int value) => new() { Top = value, Right = value, Bottom = value, Left = value };

    /// <summary>
    /// Vertical and horizontal
    /// </summary>
    public static Spacing Symmetric(int vertical = 0, int horizontal = 0) =>
        new() { Top = vertical, Right = horizontal, Bottom = vertical, Left = horizontal };

    /// <summary>
    /// Horizontal only
    /// </summary>
    public static Spacing Horizontal(int value) => new() { Right = value, Left = value };

    /// <summary>
    /// Vertical only
    /// </summary>
    public static Spacing Vertical(int value) => new() { Top = value, Bottom = value };

    /// <summary>
    /// Convert to CSS value
    /// </summary>
    public string ToCssValue()
    {
        if (Top == Right && Right == Bottom && Bottom == Left)
            return $"{Top}px";

        if (Top == Bottom && Right == Left)
            return $"{Top}px {Right}px";

        return $"{Top ?? 0}px {Right ?? 0}px {Bottom ?? 0}px {Left ?? 0}px";
    }
}

/// <summary>
/// Responsive breakpoints
/// </summary>
public class Breakpoint
{
    public int MaxWidth { get; init; }

    public static readonly Breakpoint Mobile = new() { MaxWidth = 640 };
    public static readonly Breakpoint Tablet = new() { MaxWidth = 768 };
    public static readonly Breakpoint Desktop = new() { MaxWidth = 1024 };
    public static readonly Breakpoint Wide = new() { MaxWidth = 1280 };
}
