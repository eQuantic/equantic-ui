using System;

namespace eQuantic.UI.Core.Theme.Types;

/// <summary>
/// Value Object representing a semantic color in the theme.
/// </summary>
public record ThemeColor
{
    public string Name { get; }
    public string Value { get; }

    public ThemeColor(string name, string value)
    {
        Name = name;
        Value = value;
    }

    // Common Semantic Colors
    public static readonly ThemeColor Primary = new("Primary", "bg-primary text-primary-foreground");
    public static readonly ThemeColor Secondary = new("Secondary", "bg-secondary text-secondary-foreground");
    public static readonly ThemeColor Destructive = new("Destructive", "bg-destructive text-destructive-foreground");
    public static readonly ThemeColor Muted = new("Muted", "bg-muted text-muted-foreground");
    public static readonly ThemeColor Accent = new("Accent", "bg-accent text-accent-foreground");
    
    // Implicit conversion to string for easy usage in classes
    public override string ToString() => Value;
}
