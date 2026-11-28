namespace eQuantic.UI.Primitives;

/// <summary>
/// How large a <see cref="RadialMask"/>'s ellipse is (#1938): a fraction of the box on each axis, dp on
/// each axis, or one of the four sizes CSS Images 3 names, measured from the ellipse's centre. The
/// default is <see cref="FarthestCorner"/>, CSS's.
/// <para>
/// A value with a kind, two numbers and the factories that set them, as <see cref="TileLength"/> is: a
/// closed set of cases with no data of their own would be an enum, and two of them carry a pair.
/// </para>
/// </summary>
[ZeroConstructs("vocabulary.ts: a bare `new RadialExtent()` is the farthest corner, kind and both radii at their defaults.")]
public readonly record struct RadialExtent
{
    private RadialExtent(RadialExtentKind kind, float x, float y)
    {
        Kind = kind;
        X = x;
        Y = y;
    }

    /// <summary>Which of the six sizes this is.</summary>
    public RadialExtentKind Kind { get; }

    /// <summary>The horizontal radius, as a fraction of the box's width for <see cref="RadialExtentKind.Fraction"/>
    /// and in dp for <see cref="RadialExtentKind.Dp"/>. Zero for the four named sizes.</summary>
    public float X { get; }

    /// <summary>The vertical radius, as a fraction of the box's height for <see cref="RadialExtentKind.Fraction"/>
    /// and in dp for <see cref="RadialExtentKind.Dp"/>. Zero for the four named sizes.</summary>
    public float Y { get; }

    /// <summary>The ellipse that reaches the side closest to the centre on each axis.</summary>
    public static RadialExtent ClosestSide => new(RadialExtentKind.ClosestSide, 0, 0);

    /// <summary>The ellipse that reaches the side farthest from the centre on each axis.</summary>
    public static RadialExtent FarthestSide => new(RadialExtentKind.FarthestSide, 0, 0);

    /// <summary>The ellipse that reaches the closest corner, in the proportions of <see cref="ClosestSide"/>.</summary>
    public static RadialExtent ClosestCorner => new(RadialExtentKind.ClosestCorner, 0, 0);

    /// <summary>The ellipse that reaches the farthest corner, in the proportions of <see cref="FarthestSide"/>:
    /// what <c>default</c> is.</summary>
    public static RadialExtent FarthestCorner => new(RadialExtentKind.FarthestCorner, 0, 0);

    /// <summary>Radii that are fractions of the box's width and height: <c>Fraction(0.7f, 0.7f)</c> reaches 70% of
    /// each side from the centre.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A fraction that is not a finite number above 0.</exception>
    public static RadialExtent Fraction(float x, float y) =>
        new(RadialExtentKind.Fraction, Positive(x, nameof(x)), Positive(y, nameof(y)));

    /// <summary>Radii in dp on each axis.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A radius that is not a finite number above 0.</exception>
    public static RadialExtent Dp(float x, float y) =>
        new(RadialExtentKind.Dp, Positive(x, nameof(x)), Positive(y, nameof(y)));

    private static float Positive(float radius, string parameter) => radius > 0 && float.IsFinite(radius)
        ? radius
        : throw new ArgumentOutOfRangeException(parameter, radius,
            "An ellipse's radius is a finite number above 0: a radius of nothing has no ellipse to fade along.");
}
