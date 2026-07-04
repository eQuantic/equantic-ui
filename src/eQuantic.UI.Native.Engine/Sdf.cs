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
    /// Anti-aliased coverage (0..1) from a DEVICE-space signed distance: full at ≤ −½px, zero at ≥ +½px,
    /// linear ramp across the pixel — the shader computes the same with <c>fwidth</c>-scaled distance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Coverage(float deviceDistance) =>
        Math.Clamp(0.5f - deviceDistance, 0f, 1f);
}
