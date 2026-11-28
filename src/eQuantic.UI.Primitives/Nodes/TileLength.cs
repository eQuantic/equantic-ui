namespace eQuantic.UI.Primitives;

/// <summary>
/// One side of a tile (#1937): auto (the default), a number of dp, or a fraction of the placing
/// area on its axis. An auto side takes its length from the other one and the artwork's aspect, so
/// <c>new DrawingPattern(art, 330)</c> is 330 dp wide and as tall as the artwork's shape makes it.
/// <para>
/// Not a <see cref="SizeValue"/>: its kinds are how a node asks to be laid out (hug, fill, fixed,
/// the window less an inset), and a fraction of a placing area is none of them.
/// </para>
/// </summary>
public readonly record struct TileLength
{
    private readonly bool _given;

    private TileLength(float value, bool fraction)
    {
        Value = value;
        IsFraction = fraction;
        _given = true;
    }

    /// <summary>The length in dp, or the fraction of the placing area when <see cref="IsFraction"/>.
    /// Zero when auto.</summary>
    public float Value { get; }

    /// <summary>Whether <see cref="Value"/> is a fraction of the placing area rather than dp.</summary>
    public bool IsFraction { get; }

    /// <summary>Whether the side is left to the other one and the artwork's aspect.</summary>
    public bool IsAuto => !_given;

    /// <summary>A fraction of the placing area on this side's axis: <c>0.5f</c> is half of it.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fraction outside (0, 1].</exception>
    public static TileLength Fraction(float fraction) => fraction is > 0 and <= 1
        ? new(fraction, true)
        : throw new ArgumentOutOfRangeException(nameof(fraction), fraction,
            "A tile's fraction is a part of the area it is placed in, above 0 and at most 1.");

    /// <summary>A number of dp: <c>Width = 330</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A negative or not finite length.</exception>
    [ConversionPassesThrough("The twin's patterns normalize a number through TileLength.from, so a length "
        + "crosses as the number it is and every `new DrawingPattern(art, 330)` stays a plain 330.")]
    public static implicit operator TileLength(float dp) => dp >= 0 && float.IsFinite(dp)
        ? new(dp, false)
        : throw new ArgumentOutOfRangeException(nameof(dp), dp,
            "A tile's length in dp is a finite number, zero or more.");

    /// <summary>The length on an axis whose placing area is <paramref name="extent"/> dp long.</summary>
    internal float In(float extent) => IsFraction ? Value * extent : Value;
}
