namespace eQuantic.UI.Primitives;

/// <summary>
/// Per-corner radii for a rounded rectangle, in the order TopLeft, TopRight, BottomRight, BottomLeft
/// (CSS order). A SHARED style value: components author with it, the web realizer formats
/// border-radius, the Photon engine normalizes and rasterizes it (overflowing radii clamp CSS-style
/// at render time, so <c>Radius.Full</c> is safe everywhere).
/// </summary>
public readonly record struct CornerRadii(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
{
    public static readonly CornerRadii Zero = new(0, 0, 0, 0);

    /// <summary>Uniform radius on all four corners.</summary>
    public CornerRadii(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public bool IsZero => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;

    /// <summary>All four radii reduced by <paramref name="amount"/> (floored at 0) — used when insetting
    /// a shape (e.g. drawing an inside border as a centered stroke on the deflated rect).</summary>
    public CornerRadii Deflate(float amount) => new(
        MathF.Max(0, TopLeft - amount),
        MathF.Max(0, TopRight - amount),
        MathF.Max(0, BottomRight - amount),
        MathF.Max(0, BottomLeft - amount));
}
