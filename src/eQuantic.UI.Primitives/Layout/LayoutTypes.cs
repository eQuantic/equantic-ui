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

    /// <summary>
    /// The WINDOW, less a fixed inset — <c>SizeValue.WindowMinus(88)</c> is "as tall as the window
    /// allows, minus the bar above me".
    /// <para>
    /// The one size that cannot be written as a number, because the number does not exist: an
    /// overlay that must not exceed the window and scrolls when it would is the requirement of
    /// every menu, dropdown, dialog and sheet, and any constant clips early in one window and
    /// overflows in another. Reported from a real page whose panel is 550dp: a cap of 620 leaves
    /// 215 unused at 900px and still overflows at 700px.
    /// </para>
    /// <para>
    /// Not a viewport UNIT. `vh` is the web's word and means nothing to a Photon window, so the
    /// vocabulary says what it wants — the window, less this — and each realizer answers with the
    /// window it has. Same shape as <c>WindowSizeClass</c>, which is the other axis.
    /// </para>
    /// </summary>
    WindowMinus = 3,
}

/// <summary>
/// How big a thing asks to be: <see cref="SizeKind.Hug"/> its content, <see cref="SizeKind.Fill"/>
/// what the parent offers, a <see cref="SizeKind.Fixed"/> number of dp, or
/// <see cref="SizeKind.WindowMinus"/> — the window on this axis, less an inset. A bare number
/// converts to Fixed, so <c>Width = 120</c> keeps working.
/// </summary>
public readonly record struct SizeValue(SizeKind Kind, float Value)
{
    public static readonly SizeValue Hug = new(SizeKind.Hug, 0);
    public static readonly SizeValue Fill = new(SizeKind.Fill, 0);
    public static SizeValue Fixed(float dp) => new(SizeKind.Fixed, dp);

    /// <summary>The window on this axis, less <paramref name="inset"/> dp. See
    /// <see cref="SizeKind.WindowMinus"/> for why a constant cannot express it.</summary>
    public static SizeValue WindowMinus(float inset) => inset >= 0
        ? new(SizeKind.WindowMinus, inset)
        // A negative inset asks for MORE than the window, which the name says it is not — and it
        // reaches the browser as `calc(100vh - -10px)`, which reads as a typo because it is one.
        : throw new ArgumentOutOfRangeException(nameof(inset), inset,
            "A window inset is what to SUBTRACT from the window, so it is never negative. "
            + "For more than the window, ask for the size you want.");

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
