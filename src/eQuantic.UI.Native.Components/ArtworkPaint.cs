using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The paint each shape of a <see cref="VectorDrawing"/> is filled with when the artwork is drawn
/// into <paramref name="bounds"/>: a flat colour tints the shape's coverage, a RUN fills it with the
/// same two-stop gradient the engine already paints text with, so artwork and type go through one
/// path instead of two that agree by inspection. Shared by the <see cref="Drawing"/> node and the
/// tile a <see cref="DrawingPattern"/> composites (#1937), so one artwork paints one way.
/// </summary>
/// <param name="artwork">The drawing whose shapes are painted.</param>
/// <param name="bounds">Where the artwork's viewBox is drawn, in the units the paint is in.</param>
internal sealed class ArtworkPaint(VectorDrawing artwork, Rect bounds)
{
    /// <summary>The paint of <paramref name="paint"/> on <paramref name="shape"/>, with
    /// <paramref name="tint"/> answering an inherited one.</summary>
    public Paint For(VectorShape shape, VectorPaint paint, Color tint)
    {
        var flat = Paint.Solid(paint.Resolve(tint).WithOpacity(shape.Opacity));
        if (!paint.IsGradient) return flat;

        var run = paint.Gradient!.Value;
        if (run.Stops.Count == 0) return flat;
        // Two stops, first and last. The engine's paint interpolates between a pair —
        // multi-stop is its own slice (a stop pool the shader indexes), and until then
        // a three-stop run arrives as its ends rather than as nothing.
        var from = run.Stops[0].Color.WithOpacity(shape.Opacity * run.Stops[0].Color.A / 255f);
        var last = run.Stops[^1].Color;
        var to = last.WithOpacity(shape.Opacity * last.A / 255f);

        var box = Frame(shape, run);
        return paint.Kind == VectorPaintKind.RadialGradient
            ? Paint.Radial(
                new Point(box.X + run.X1 * box.Width, box.Y + run.Y1 * box.Height),
                run.Radius * box.Width, run.Radius * box.Height, from, to, Svg)
            : Paint.Linear(
                new Point(box.X + run.X1 * box.Width, box.Y + run.Y1 * box.Height),
                new Point(box.X + run.X2 * box.Width, box.Y + run.Y2 * box.Height),
                from, to, Svg);
    }

    // An artwork's runs are SVG paint servers on the web, and Chromium 156 mixes their stops straight:
    // red into transparent black reads (191, 128, 128) over white, where a CSS gradient reads (255, 128,
    // 128). So the artwork keeps the straight mix a box's gradient left (#1956).
    private const GradientInterpolation Svg = GradientInterpolation.Straight;

    // Where the run's fractions are measured from. SVG's default is the SHAPE's own box
    // (objectBoundingBox), which is why the path has to answer for its own bounds;
    // userSpaceOnUse measures on the viewBox grid, so the whole drawing's box is the
    // frame and the coordinates arrive already divided by it.
    private Rect Frame(VectorShape shape, VectorGradient run)
    {
        if (run.UserSpace)
        {
            var scaleX = artwork.Width > 0 ? bounds.Width / artwork.Width : 1;
            var scaleY = artwork.Height > 0 ? bounds.Height / artwork.Height : 1;
            return new Rect(
                bounds.X - artwork.MinX * scaleX, bounds.Y - artwork.MinY * scaleY,
                scaleX, scaleY);
        }

        var (minX, minY, maxX, maxY) = VectorPath.Bounds(shape.Path);
        var unitX = artwork.Width > 0 ? bounds.Width / artwork.Width : 1;
        var unitY = artwork.Height > 0 ? bounds.Height / artwork.Height : 1;
        return new Rect(
            bounds.X + (minX - artwork.MinX) * unitX,
            bounds.Y + (minY - artwork.MinY) * unitY,
            MathF.Max(maxX - minX, 0) * unitX,
            MathF.Max(maxY - minY, 0) * unitY);
    }
}
