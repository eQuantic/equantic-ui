namespace eQuantic.UI.Primitives;

/// <summary>
/// A mask whose opacity runs outward from a point along an ellipse (#1938): the offset of a stop is the
/// fraction of the ellipse's radius, so 0 is the centre and 1 is the ellipse's edge. The centre is a
/// fraction of the box on each axis, the rule <see cref="ArtworkPattern.Anchor"/> and <see cref="Image.FocalPoint"/>
/// follow, and the ellipse is sized by a <see cref="RadialExtent"/>.
/// </summary>
public sealed record RadialOpacityGradient : OpacityGradient
{
    public RadialOpacityGradient(Point center, RadialExtent extent, IReadOnlyList<OpacityStop> stops)
        : base(stops)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center), center,
                "A radial mask's centre is a finite fraction of the box on each axis: (0.5, 0.5) is its middle.");
        Center = center;
        Extent = extent;
    }

    /// <summary>The ellipse's centre as fractions of the box on each axis: (0.5, 0.5) is the middle.</summary>
    public Point Center { get; }

    /// <summary>How large the ellipse is.</summary>
    public RadialExtent Extent { get; }

    /// <summary>
    /// The ellipse's two radii in dp on a box of <paramref name="box"/>, as CSS Images 3 sizes it. A radius is
    /// zero where the centre sits on a side that <see cref="RadialExtentKind.ClosestSide"/> reaches: CSS's
    /// degenerate ellipse, where the opacity is the last stop's everywhere off the centre line.
    /// </summary>
    [ServerOnly]
    public Size Radii(Size box)
    {
        var x = Center.X * box.Width;
        var y = Center.Y * box.Height;
        var closest = new Size(MathF.Min(x, box.Width - x), MathF.Min(y, box.Height - y));
        var farthest = new Size(MathF.Max(x, box.Width - x), MathF.Max(y, box.Height - y));
        return Extent.Kind switch
        {
            RadialExtentKind.Fraction => new Size(Extent.X * box.Width, Extent.Y * box.Height),
            RadialExtentKind.Dp => new Size(Extent.X, Extent.Y),
            RadialExtentKind.ClosestSide => closest,
            RadialExtentKind.FarthestSide => farthest,
            RadialExtentKind.ClosestCorner => new Size(closest.Width * MathF.Sqrt(2), closest.Height * MathF.Sqrt(2)),
            _ => new Size(farthest.Width * MathF.Sqrt(2), farthest.Height * MathF.Sqrt(2)),
        };
    }

    private protected override TResult Dispatch<TState, TResult>(IOpacityGradientVisitor<TState, TResult> visitor, TState state) =>
        visitor.Visit(this, state);
}
