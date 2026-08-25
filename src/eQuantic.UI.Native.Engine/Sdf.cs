using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// The NORMATIVE signed-distance-field math of the Photon engine (plan D2). Every rasterizer — the
/// CPU reference backend and the Slang fragment shaders — implements exactly these formulas, so the
/// golden-image harness can compare their output pixel-for-pixel. Change here = change the shaders =
/// regenerate goldens; treat this file as a spec.
///
/// Conventions: distances are in the shape's LOCAL space; negative = inside, positive = outside,
/// zero = the exact edge. Y grows down.
/// </summary>
public static class Sdf
{
    /// <summary>
    /// Signed distance from point <paramref name="p"/> (relative to the rrect CENTER) to a rounded
    /// rectangle of half-size <paramref name="halfSize"/> with per-corner radii — radii MUST be
    /// normalized (<see cref="RRect.Normalized"/>). The classic per-corner formula: select the active
    /// corner radius by quadrant, then
    /// <c>q = |p| − (halfSize − r);  d = min(max(q.x, q.y), 0) + length(max(q, 0)) − r</c>.
    /// Radius 0 degenerates to a sharp rect corner; radius = halfSize degenerates to a circle/pill.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float RoundedRect(Point p, Size halfSize, CornerRadii radii)
    {
        // Quadrant → corner (y grows down): (-,-)=TL  (+,-)=TR  (+,+)=BR  (-,+)=BL.
        var r = p.X >= 0
            ? (p.Y >= 0 ? radii.BottomRight : radii.TopRight)
            : (p.Y >= 0 ? radii.BottomLeft : radii.TopLeft);

        var qx = MathF.Abs(p.X) - (halfSize.Width - r);
        var qy = MathF.Abs(p.Y) - (halfSize.Height - r);

        var outsideX = MathF.Max(qx, 0);
        var outsideY = MathF.Max(qy, 0);
        var outside = MathF.Sqrt(outsideX * outsideX + outsideY * outsideY);
        var inside = MathF.Min(MathF.Max(qx, qy), 0);
        return outside + inside - r;
    }

    /// <summary>
    /// Signed distance to the CENTERED stroke of a shape: the band of width <paramref name="strokeWidth"/>
    /// straddling the fill edge — <c>|d| − strokeWidth / 2</c>. (CSS-style INNER borders are produced at
    /// the component layer by insetting the shape by half the width first; the engine primitive stays pure.)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Stroke(float distance, float strokeWidth) =>
        MathF.Abs(distance) - strokeWidth / 2;

    /// <summary>
    /// Analytic shadow falloff (spec §05): a smooth ramp of the rrect SDF across the blur radius —
    /// <c>1 − smoothstep(−1.5σ, +1.5σ, d)</c> with <c>σ = blur/2</c>. A normative CHOICE (a Gaussian
    /// approximation), implemented identically by every rasterizer; blur 0 degenerates to the hard
    /// coverage ramp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ShadowCoverage(float deviceDistance, float deviceBlur)
    {
        if (deviceBlur <= 0) return Coverage(deviceDistance);
        var sigma = deviceBlur / 2;
        var t = Math.Clamp((deviceDistance + 1.5f * sigma) / (3f * sigma), 0f, 1f);
        return 1f - t * t * (3f - 2f * t); // 1 − smoothstep
    }

    /// <summary>
    /// Anti-aliased coverage (0..1) from a DEVICE-space signed distance: full at ≤ −½px, zero at ≥ +½px,
    /// linear ramp across the pixel — the shader computes the same with <c>fwidth</c>-scaled distance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Coverage(float deviceDistance) =>
        Math.Clamp(0.5f - deviceDistance, 0f, 1f);

    /// <summary>
    /// Signed distance to an ANNULAR SECTOR (ring slice) centered at the origin — the W1 primitive
    /// (docs/DESKTOP-PLAN.md): sunburst arcs, ring gauges, donut charts. EXACT, derived from the
    /// folded geometry rather than an intersection bound, so the rounded variant stays a true SDF:
    /// <para>
    /// Angles are RADIANS, 0 along +X, increasing toward +Y — y grows down, so angles run CLOCKWISE
    /// on screen. The sector spans <paramref name="startAngle"/>→<paramref name="endAngle"/>
    /// (sweep ≤ 2π; a full ring is sweep 2π with rounding 0 — rounding at 2π rounds the seam).
    /// <paramref name="rounding"/> rounds all four corners by insetting every boundary and
    /// subtracting: the radii move in by ρ, the straight edges move in PERPENDICULARLY by ρ (an
    /// euclidean inset, not an angular one — an angular shift would round thin inner corners more
    /// than outer ones), and the final distance is the inset shape's minus ρ.
    /// </para>
    /// <para>
    /// The fold: rotate by −mid so the sector is symmetric about +X, mirror to the upper half. With
    /// w = the perpendicular distance to the edge line (inside-positive), a point angularly inside
    /// the inset wedge (w ≥ ρ) sees only radial boundaries and that line — <c>max(rIn−r, r−rOut,
    /// ρ−w)</c> is the exact signed distance; a point beyond it sees the inset edge SEGMENT, whose
    /// endpoints sit where the inset line meets the inset arcs (<c>s = √(r² − ρ²)</c>).
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AnnularSector(Point p, float innerRadius, float outerRadius,
        float startAngle, float endAngle, float rounding)
    {
        var rIn = innerRadius + rounding;
        var rOut = outerRadius - rounding;
        var mid = (startAngle + endAngle) / 2;
        var half = (endAngle - startAngle) / 2;

        var cosMid = MathF.Cos(mid);
        var sinMid = MathF.Sin(mid);
        var qx = cosMid * p.X + sinMid * p.Y;
        var qy = MathF.Abs(-sinMid * p.X + cosMid * p.Y);
        var r = MathF.Sqrt(qx * qx + qy * qy);

        var sinHalf = MathF.Sin(half);
        var cosHalf = MathF.Cos(half);
        var w = qx * sinHalf - qy * cosHalf;

        float d;
        if (w >= rounding)
        {
            d = MathF.Max(MathF.Max(rIn - r, r - rOut), rounding - w);
        }
        else
        {
            var s = Math.Clamp(qx * cosHalf + qy * sinHalf,
                MathF.Sqrt(MathF.Max(rIn * rIn - rounding * rounding, 0f)),
                MathF.Sqrt(MathF.Max(rOut * rOut - rounding * rounding, 0f)));
            var ex = s * cosHalf + rounding * sinHalf;
            var ey = s * sinHalf - rounding * cosHalf;
            var dx = qx - ex;
            var dy = qy - ey;
            d = MathF.Sqrt(dx * dx + dy * dy);
        }
        return d - rounding;
    }
}
