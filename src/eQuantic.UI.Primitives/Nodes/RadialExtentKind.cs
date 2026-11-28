namespace eQuantic.UI.Primitives;

/// <summary>
/// How a <see cref="RadialExtent"/> sizes an ellipse (#1938): by one of the four sizes CSS Images 3
/// names, from the point the ellipse is centred on, or by an explicit radius on each axis.
/// <para>
/// APPEND-ONLY, as every enum of the vocabulary is: a constant is baked into the assembly that reads
/// it, so renumbering is silent (<c>EnumValueAbiTests</c>).
/// </para>
/// </summary>
public enum RadialExtentKind : byte
{
    /// <summary>The ellipse that reaches the farthest corner, in the proportions of
    /// <see cref="FarthestSide"/>: the default, and CSS's.</summary>
    FarthestCorner = 0,

    /// <summary>The ellipse that reaches the side closest to the centre on each axis.</summary>
    ClosestSide = 1,

    /// <summary>The ellipse that reaches the side farthest from the centre on each axis.</summary>
    FarthestSide = 2,

    /// <summary>The ellipse that reaches the closest corner, in the proportions of
    /// <see cref="ClosestSide"/>.</summary>
    ClosestCorner = 3,

    /// <summary>A radius on each axis as a fraction of the box's own side on that axis.</summary>
    Fraction = 4,

    /// <summary>A radius on each axis in dp.</summary>
    Dp = 5,
}
