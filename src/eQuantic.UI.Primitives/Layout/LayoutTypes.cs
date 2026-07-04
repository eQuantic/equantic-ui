namespace eQuantic.UI.Primitives;

/// <summary>
/// Padding insets (spec §03: Photon has NO margin — outer space belongs to the parent's Gap/Padding).
/// Start/End instead of Left/Right so RTL mirroring is a realizer concern (v1 maps Start→left).
/// </summary>
public readonly record struct EdgeInsets(float Start, float Top, float End, float Bottom)
{
    public static readonly EdgeInsets Zero = default;

    public static EdgeInsets All(float value) => new(value, value, value, value);
    public static EdgeInsets Symmetric(float horizontal, float vertical) =>
        new(horizontal, vertical, horizontal, vertical);

    public float Horizontal => Start + End;
    public float Vertical => Top + Bottom;
}

public enum SizeKind : byte
{
    /// <summary>Size to content (the default — spec A1 resolution order: explicit &gt; Fill &gt; Hug).</summary>
    Hug = 0,
    /// <summary>Fill the space the parent offers (collapses to Hug when the parent is unbounded).</summary>
    Fill = 1,
    /// <summary>An explicit dp value.</summary>
    Fixed = 2,
}

/// <summary>One dimension of a node's requested size: Hug (default) · Fill · explicit dp.</summary>
public readonly record struct SizeValue(SizeKind Kind, float Value)
{
    public static readonly SizeValue Hug = new(SizeKind.Hug, 0);
    public static readonly SizeValue Fill = new(SizeKind.Fill, 0);
    public static SizeValue Fixed(float dp) => new(SizeKind.Fixed, dp);

    /// <summary>A bare number is an explicit size — <c>Width = 120</c>.</summary>
    public static implicit operator SizeValue(float dp) => Fixed(dp);
}

/// <summary>Main-axis distribution (spec A2 — Around/Evenly are cut from v1: use Spacers).</summary>
public enum MainAlign : byte
{
    Start = 0,
    Center = 1,
    End = 2,
    SpaceBetween = 3,
}

/// <summary>Cross-axis alignment (spec A2 — Row defaults to Center, Column to Stretch).</summary>
public enum CrossAlign : byte
{
    Start = 0,
    Center = 1,
    End = 2,
    Stretch = 3,
}
