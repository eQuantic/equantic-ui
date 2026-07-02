using System.Runtime.CompilerServices;

namespace eQuantic.UI.Native.Engine;

/// <summary>A point (or vector) in 2D space. Y grows DOWN — screen convention, used everywhere in Photon.</summary>
public readonly record struct Point(float X, float Y)
{
    public static readonly Point Zero = new(0, 0);

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    public static Point operator *(Point a, float s) => new(a.X * s, a.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dot(Point other) => X * other.X + Y * other.Y;

    public float Length() => MathF.Sqrt(X * X + Y * Y);
}

/// <summary>A 2D size. Negative dimensions are invalid by construction convention (not enforced per-op).</summary>
public readonly record struct Size(float Width, float Height)
{
    public static readonly Size Zero = new(0, 0);
}

/// <summary>An axis-aligned rectangle (X/Y = top-left corner; Y grows down).</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Point Center => new(X + Width / 2, Y + Height / 2);
    public Size Size => new(Width, Height);

    public static Rect FromLTRB(float left, float top, float right, float bottom) =>
        new(left, top, right - left, bottom - top);

    public bool Contains(Point p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    /// <summary>The intersection with <paramref name="other"/>, or an empty rect when disjoint.</summary>
    public Rect Intersect(Rect other)
    {
        var l = MathF.Max(Left, other.Left);
        var t = MathF.Max(Top, other.Top);
        var r = MathF.Min(Right, other.Right);
        var b = MathF.Min(Bottom, other.Bottom);
        return r <= l || b <= t ? new Rect(l, t, 0, 0) : FromLTRB(l, t, r, b);
    }

    /// <summary>Expands the rect by <paramref name="amount"/> on every side (negative insets).</summary>
    public Rect Inflate(float amount) =>
        new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// Per-corner radii for a rounded rectangle, in the order TopLeft, TopRight, BottomRight, BottomLeft
/// (CSS order). Use <see cref="RRect.Normalized"/> before rasterizing — raw radii may overflow the box.
/// </summary>
public readonly record struct CornerRadii(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
{
    public static readonly CornerRadii Zero = new(0, 0, 0, 0);

    /// <summary>Uniform radius on all four corners.</summary>
    public CornerRadii(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public bool IsZero => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;
}

/// <summary>A rounded rectangle — the workhorse UI shape (radius 0 = rect, radius = half-size = circle/pill).</summary>
public readonly record struct RRect(Rect Rect, CornerRadii Radii)
{
    public RRect(Rect rect) : this(rect, CornerRadii.Zero) { }

    /// <summary>
    /// Clamps the radii so adjacent pairs never exceed the side they share, preserving their ratio —
    /// the CSS border-radius overflow rule: <c>scale = min(side / (r1 + r2))</c> over all four sides,
    /// applied to every radius when &lt; 1. Also floors negatives at 0. Rasterizers require normalized
    /// radii; callers may hold un-normalized values freely.
    /// </summary>
    public RRect Normalized()
    {
        var tl = MathF.Max(0, Radii.TopLeft);
        var tr = MathF.Max(0, Radii.TopRight);
        var br = MathF.Max(0, Radii.BottomRight);
        var bl = MathF.Max(0, Radii.BottomLeft);

        var w = Rect.Width;
        var h = Rect.Height;
        var scale = 1f;
        if (tl + tr > 0) scale = MathF.Min(scale, w / (tl + tr));
        if (bl + br > 0) scale = MathF.Min(scale, w / (bl + br));
        if (tl + bl > 0) scale = MathF.Min(scale, h / (tl + bl));
        if (tr + br > 0) scale = MathF.Min(scale, h / (tr + br));

        if (scale < 1f)
        {
            tl *= scale; tr *= scale; br *= scale; bl *= scale;
        }
        return new RRect(Rect, new CornerRadii(tl, tr, br, bl));
    }
}

/// <summary>
/// A 2D affine transform (row-vector convention: <c>p' = p · M</c>):
/// <code>
/// | M11 M12 0 |
/// | M21 M22 0 |
/// | M31 M32 1 |
/// </code>
/// M31/M32 carry translation. Composition <c>A * B</c> applies A first, then B.
/// </summary>
public readonly record struct Matrix2D(float M11, float M12, float M21, float M22, float M31, float M32)
{
    public static readonly Matrix2D Identity = new(1, 0, 0, 1, 0, 0);

    public static Matrix2D Translation(float x, float y) => new(1, 0, 0, 1, x, y);
    public static Matrix2D Scale(float sx, float sy) => new(sx, 0, 0, sy, 0, 0);

    public static Matrix2D Rotation(float radians)
    {
        var c = MathF.Cos(radians);
        var s = MathF.Sin(radians);
        return new Matrix2D(c, s, -s, c, 0, 0);
    }

    public bool IsIdentity => this == Identity;

    /// <summary>Applies <paramref name="a"/> first, then <paramref name="b"/>.</summary>
    public static Matrix2D operator *(Matrix2D a, Matrix2D b) => new(
        a.M11 * b.M11 + a.M12 * b.M21,
        a.M11 * b.M12 + a.M12 * b.M22,
        a.M21 * b.M11 + a.M22 * b.M21,
        a.M21 * b.M12 + a.M22 * b.M22,
        a.M31 * b.M11 + a.M32 * b.M21 + b.M31,
        a.M31 * b.M12 + a.M32 * b.M22 + b.M32);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point Transform(Point p) => new(
        p.X * M11 + p.Y * M21 + M31,
        p.X * M12 + p.Y * M22 + M32);

    public float Determinant => M11 * M22 - M12 * M21;

    /// <summary>
    /// The inverse transform, or <c>null</c> for a degenerate (non-invertible) matrix — callers skip
    /// drawing a shape whose transform collapses it to zero area.
    /// </summary>
    public Matrix2D? Invert()
    {
        var det = Determinant;
        if (MathF.Abs(det) < 1e-9f) return null;
        var inv = 1f / det;
        var m11 = M22 * inv;
        var m12 = -M12 * inv;
        var m21 = -M21 * inv;
        var m22 = M11 * inv;
        return new Matrix2D(
            m11, m12, m21, m22,
            -(M31 * m11 + M32 * m21),
            -(M31 * m12 + M32 * m22));
    }

    /// <summary>
    /// The average absolute scale factor — <c>sqrt(|det|)</c>. Exact for uniform scale + rotation;
    /// an approximation under non-uniform scale. Used to convert local-space SDF distances to device
    /// pixels for anti-aliasing width (the GPU shader uses <c>fwidth</c>, which is exact — golden
    /// comparisons tolerate the residual difference at non-uniformly-scaled edges).
    /// </summary>
    public float AverageScale() => MathF.Sqrt(MathF.Abs(Determinant));

    /// <summary>Device-space axis-aligned bounds of <paramref name="rect"/> under this transform.</summary>
    public Rect TransformBounds(Rect rect)
    {
        var p0 = Transform(new Point(rect.Left, rect.Top));
        var p1 = Transform(new Point(rect.Right, rect.Top));
        var p2 = Transform(new Point(rect.Right, rect.Bottom));
        var p3 = Transform(new Point(rect.Left, rect.Bottom));
        var minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        var minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        var maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        var maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));
        return Rect.FromLTRB(minX, minY, maxX, maxY);
    }
}
