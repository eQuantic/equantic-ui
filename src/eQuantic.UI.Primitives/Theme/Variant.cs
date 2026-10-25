namespace eQuantic.UI.Primitives;

/// <summary>Interactive variants — conceptual parity with the web SDK's Variant set (spec §01/§09).</summary>
public enum Variant : byte
{
    Primary = 0,
    Secondary = 1,
    Destructive = 2,
    Outline = 3,
    Ghost = 4,
    Link = 5,
    Success = 6,
    Warning = 7,
    Info = 8,
    /// <summary>The M3 tertiary role — a contrasting accent for balance. Photon tones it from Info.</summary>
    Tertiary = 9,
}
