using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a press on an input shape of a <see cref="Drawing"/> is tested against: the shape's own path
/// on the artwork's grid, its fill rule, and what takes a point of the box the drawing was laid out in
/// onto that grid. A shape is pressed where it is painted, never across its bounding box: a concelho's
/// box holds half of its neighbours (#674). Flutter answers the same question with
/// <c>Path.contains</c> inside a <c>CustomPainter.hitTest</c>.
/// </summary>
/// <param name="Segments">The path, parsed once, on the artwork's grid.</param>
/// <param name="EvenOdd">The shape's fill rule: even-odd where it says so, nonzero otherwise.</param>
/// <param name="ToArtwork">A point of the drawing's laid-out box, taken onto the artwork's grid.</param>
internal readonly record struct ShapeHit(IReadOnlyList<VectorSegment> Segments, bool EvenOdd, Matrix2D ToArtwork)
{
    // A cubic is tested as this many chords. A concelho's border drawn at a few hundred dp is within a
    // fraction of a dp of its curve at sixteen, and a press is a finger, not a pixel.
    private const int ChordsPerCubic = 16;

    // Each path read once, not every frame: Photon builds every frame, and a map's paths, its press
    // test and its semantics would each parse every concelho again. Keyed by the string itself, which
    // an app's data keeps from one frame to the next, and let go with it.
    private static readonly ConditionalWeakTable<string, Parsed> Paths = new();

    private sealed record Parsed(IReadOnlyList<VectorSegment> Segments, float MinX, float MinY, float MaxX, float MaxY);

    private static Parsed Read(string path) => Paths.GetValue(path, data =>
    {
        var (minX, minY, maxX, maxY) = VectorPath.Bounds(data);
        return new Parsed(VectorPath.Parse(data), minX, minY, maxX, maxY);
    });

    /// <summary>The test for the <paramref name="index"/>th shape of <paramref name="drawing"/>,
    /// laid out in <paramref name="box"/>.</summary>
    public static ShapeHit For(Drawing drawing, int index, Rect box)
    {
        var shape = drawing.Artwork.Shapes[index];
        return new ShapeHit(Read(shape.Path).Segments, shape.EvenOdd, ToArtworkOf(drawing.Artwork, box));
    }

    /// <summary>
    /// Where the shape is drawn in <paramref name="box"/>: the hull of its path mapped the way the
    /// drawing is drawn, each axis scaled on its own, as the web's <c>preserveAspectRatio="none"</c>
    /// and the rasterizer both scale it. Its focus ring, its semantic node and its target stand there.
    /// </summary>
    public static Rect BoundsOf(VectorDrawing artwork, VectorShape shape, Rect box)
    {
        var (_, minX, minY, maxX, maxY) = Read(shape.Path);
        var unitX = artwork.Width > 0 ? box.Width / artwork.Width : 1;
        var unitY = artwork.Height > 0 ? box.Height / artwork.Height : 1;
        return new Rect(
            box.X + (minX - artwork.MinX) * unitX,
            box.Y + (minY - artwork.MinY) * unitY,
            MathF.Max(maxX - minX, 0) * unitX,
            MathF.Max(maxY - minY, 0) * unitY);
    }

    /// <summary>Whether <paramref name="point"/>, in the drawing's laid-out box, lands inside the path
    /// by its fill rule.</summary>
    public bool Contains(Point point)
    {
        var at = ToArtwork.Transform(point);
        var winding = 0;
        var start = default(Point);
        var current = default(Point);
        var open = false;

        foreach (var segment in Segments)
        {
            switch (segment.Verb)
            {
                case VectorVerb.Move:
                    // A fill closes every subpath it paints, so an open one is closed here too.
                    if (open) winding += Crossing(current, start, at);
                    start = current = segment.End;
                    open = true;
                    break;
                case VectorVerb.Line:
                    winding += Crossing(current, segment.End, at);
                    current = segment.End;
                    break;
                case VectorVerb.Cubic:
                    var from = current;
                    for (var i = 1; i <= ChordsPerCubic; i++)
                    {
                        var to = PointOn(from, segment.C1, segment.C2, segment.End, (float)i / ChordsPerCubic);
                        winding += Crossing(current, to, at);
                        current = to;
                    }
                    break;
                case VectorVerb.Close:
                    winding += Crossing(current, start, at);
                    current = start;
                    open = false;
                    break;
            }
        }
        if (open) winding += Crossing(current, start, at);
        return EvenOdd ? (winding & 1) != 0 : winding != 0;
    }

    /// <summary>The edge's part of the winding number around <paramref name="point"/>: +1 crossing
    /// upward with the point on its left, -1 crossing downward with the point on its right, 0 otherwise.
    /// The parity of the sum is the even-odd answer, and the sum itself the nonzero one.</summary>
    private static int Crossing(Point a, Point b, Point point)
    {
        var side = (b.X - a.X) * (point.Y - a.Y) - (point.X - a.X) * (b.Y - a.Y);
        if (a.Y <= point.Y)
            return b.Y > point.Y && side > 0 ? 1 : 0;
        return b.Y <= point.Y && side < 0 ? -1 : 0;
    }

    private static Point PointOn(Point p0, Point p1, Point p2, Point p3, float t)
    {
        var u = 1 - t;
        var a = u * u * u;
        var b = 3 * u * u * t;
        var c = 3 * u * t * t;
        var d = t * t * t;
        return new Point(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
    }

    /// <summary>What takes a point of <paramref name="box"/> onto the artwork's grid: the inverse of
    /// how the drawing is drawn, each axis on its own.</summary>
    private static Matrix2D ToArtworkOf(VectorDrawing artwork, Rect box)
    {
        var scaleX = box.Width > 0 ? artwork.Width / box.Width : 0;
        var scaleY = box.Height > 0 ? artwork.Height / box.Height : 0;
        return new Matrix2D(scaleX, 0, 0, scaleY, artwork.MinX - box.X * scaleX, artwork.MinY - box.Y * scaleY);
    }
}
