namespace eQuantic.UI.Primitives;

/// <summary>
/// A mask whose opacity runs along a straight line (#1938): the angle is in degrees clockwise from
/// "toward the top", so 0 runs bottom to top, 90 left to right, 180 top to bottom. The gradient's line
/// passes through the box's centre and is as long as its ends touching the box's corners makes it, as
/// CSS and every design tool define a linear gradient.
/// </summary>
public sealed record LinearMask : Mask
{
    public LinearMask(float angle, IReadOnlyList<MaskStop> stops)
        : base(stops)
    {
        if (!float.IsFinite(angle))
            throw new ArgumentOutOfRangeException(nameof(angle), angle,
                "A linear mask's angle is a finite number of degrees, clockwise from toward the top.");
        Angle = angle;
    }

    /// <summary>The direction of the gradient in degrees, clockwise from "toward the top".</summary>
    public float Angle { get; }

    /// <summary>
    /// The two ends of the gradient's line on a box of <paramref name="box"/>, where the first stop's offset 0 and
    /// the last's offset 1 stand: through the centre, along (sin θ, −cos θ), with the length
    /// |W·sin θ| + |H·cos θ| that makes its ends touch the corners.
    /// </summary>
    [ServerOnly]
    public (Point Start, Point End) Axis(Size box)
    {
        var radians = Angle * MathF.PI / 180;
        var (sin, cos) = MathF.SinCos(radians);
        var length = MathF.Abs(box.Width * sin) + MathF.Abs(box.Height * cos);
        var half = new Point(sin * length / 2, -cos * length / 2);
        var centre = new Point(box.Width / 2, box.Height / 2);
        return (new Point(centre.X - half.X, centre.Y - half.Y), new Point(centre.X + half.X, centre.Y + half.Y));
    }

    private protected override TResult Dispatch<TState, TResult>(IMaskVisitor<TState, TResult> visitor, TState state) =>
        visitor.Visit(this, state);
}
