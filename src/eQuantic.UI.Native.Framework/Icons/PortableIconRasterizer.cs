using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// An <see cref="IIconRasterizer"/> in plain C#, for a host with no platform rasterizer: the build of
/// an app icon on any machine, Linux included, where every container image is built (#1240). Without
/// one the engine draws a <see cref="Drawing"/> or an <see cref="Icon"/> as its placeholder box, so an
/// icon that drew the app's own SVG came out a grey square, and only on the machines that build
/// releases.
/// <para>
/// It answers as the platform ones do (<c>CoreGraphicsIconRasterizer</c> is the reference): the path
/// lowered by <see cref="VectorPath"/> to moves, lines and cubics, filled by the nonzero rule, or
/// stroked with round caps and joins, the icon-pack convention, into A8 coverage at device scale, each
/// axis scaled on its own so a stroke follows the box as the platform's transform makes it.
/// </para>
/// <para>
/// Coverage is EXACT AREA, not samples: each edge adds the signed area it leaves in each pixel of its
/// row, and a running sum along the row turns those into coverage (the accumulation rasterizers of
/// fonts use). A stroke is its outline's union, a quad along each piece and a disc at every vertex,
/// each turned the same way so the sum only ever adds; the clamp at full coverage is the union.
/// </para>
/// </summary>
public sealed class PortableIconRasterizer : IIconRasterizer
{
    /// <summary>How finely a curve is cut, in device pixels of length per piece.</summary>
    private const float Flatness = 0.75f;

    /// <inheritdoc />
    public TextRaster? Rasterize(IconGlyph glyph, float widthDp, float heightDp, float scale)
    {
        var segments = VectorPath.Parse(glyph.Path);
        if (segments.Count == 0 || !TryViewBox(glyph.ViewBox, out var minX, out var minY, out var unitW, out var unitH))
            return null;

        var width = Math.Max(1, (int)MathF.Ceiling(widthDp * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(heightDp * scale));
        var sx = width / unitW;
        var sy = height / unitH;
        Point ToPixels(Point p) => new((p.X - minX) * sx, (p.Y - minY) * sy);

        var paths = Flatten(segments, MathF.Max(sx, sy));
        var canvas = new Coverage(width, height);
        if (glyph.Style == IconGlyphStyle.Stroke)
        {
            foreach (var outline in StrokeOutlines(paths, glyph.StrokeWidth / 2, MathF.Max(sx, sy)))
                canvas.AddPolygon(outline.Select(ToPixels).ToList());
        }
        else
        {
            foreach (var path in paths)
                canvas.AddPolygon(path.Points.Select(ToPixels).ToList());
        }

        return new TextRaster(width, height, canvas.ToAlpha());
    }

    /// <summary>The viewBox as numbers, "minX minY width height", invariant: a comma is SVG's separator.</summary>
    private static bool TryViewBox(string viewBox, out float minX, out float minY, out float width, out float height)
    {
        minX = minY = width = height = 0;
        var parts = viewBox.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 4
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out minX)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out minY)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out width)
            && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out height)
            && width > 0 && height > 0;
    }

    /// <summary>A subpath as points in the glyph's units, and whether it was closed.</summary>
    private sealed record Polyline(List<Point> Points, bool Closed);

    /// <summary>
    /// Every subpath as a polyline: a cubic cut into pieces of about <see cref="Flatness"/> device
    /// pixels, judged by its control polygon's length, which is never shorter than the curve.
    /// </summary>
    private static List<Polyline> Flatten(IReadOnlyList<VectorSegment> segments, float unitsToPixels)
    {
        var paths = new List<Polyline>();
        List<Point>? current = null;
        var start = default(Point);
        var at = default(Point);

        void Finish(bool closed)
        {
            if (current is { Count: > 1 }) paths.Add(new Polyline(current, closed));
            current = null;
        }

        foreach (var segment in segments)
        {
            switch (segment.Verb)
            {
                case VectorVerb.Move:
                    Finish(closed: false);
                    start = at = segment.End;
                    current = [at];
                    break;
                case VectorVerb.Line:
                    current ??= [at];
                    current.Add(segment.End);
                    at = segment.End;
                    break;
                case VectorVerb.Cubic:
                {
                    current ??= [at];
                    var length = Distance(at, segment.C1) + Distance(segment.C1, segment.C2) + Distance(segment.C2, segment.End);
                    var pieces = Math.Clamp((int)MathF.Ceiling(length * unitsToPixels / Flatness), 1, 512);
                    for (var i = 1; i <= pieces; i++)
                        current.Add(Cubic(at, segment.C1, segment.C2, segment.End, (float)i / pieces));
                    at = segment.End;
                    break;
                }
                case VectorVerb.Close:
                    if (current is not null && current[^1] != start) current.Add(start);
                    Finish(closed: true);
                    at = start;
                    break;
            }
        }
        Finish(closed: false);
        return paths;
    }

    private static Point Cubic(Point p0, Point p1, Point p2, Point p3, float t)
    {
        var u = 1 - t;
        float Axis(float a, float b, float c, float d) => u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        return new Point(Axis(p0.X, p1.X, p2.X, p3.X), Axis(p0.Y, p1.Y, p2.Y, p3.Y));
    }

    private static float Distance(Point a, Point b) => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    /// <summary>
    /// The outline of a stroke of half-width <paramref name="radius"/>, in glyph units, as polygons
    /// whose union is the stroke: a quad along each piece and a disc at each vertex, which are the
    /// round joins between pieces and the round caps at an open path's ends. Each is turned the same
    /// way, so where two overlap their coverage adds rather than cancels.
    /// </summary>
    private static IEnumerable<List<Point>> StrokeOutlines(IEnumerable<Polyline> paths, float radius, float unitsToPixels)
    {
        if (radius <= 0) yield break;
        var sides = Math.Clamp((int)MathF.Ceiling(2 * MathF.PI * radius * unitsToPixels / Flatness), 8, 96);
        foreach (var path in paths)
        {
            var points = path.Points;
            for (var i = 0; i < points.Count; i++)
            {
                yield return Turned(Disc(points[i], radius, sides));
                if (i == 0) continue;
                var (a, b) = (points[i - 1], points[i]);
                var length = Distance(a, b);
                if (length <= 0) continue;
                var nx = -(b.Y - a.Y) / length * radius;
                var ny = (b.X - a.X) / length * radius;
                yield return Turned([
                    new Point(a.X + nx, a.Y + ny), new Point(b.X + nx, b.Y + ny),
                    new Point(b.X - nx, b.Y - ny), new Point(a.X - nx, a.Y - ny),
                ]);
            }
        }
    }

    private static List<Point> Disc(Point centre, float radius, int sides)
    {
        var disc = new List<Point>(sides);
        for (var i = 0; i < sides; i++)
        {
            var angle = 2 * MathF.PI * i / sides;
            disc.Add(new Point(centre.X + radius * MathF.Cos(angle), centre.Y + radius * MathF.Sin(angle)));
        }
        return disc;
    }

    /// <summary>The polygon turned one way, whichever way it came: by the sign of its area.</summary>
    private static List<Point> Turned(List<Point> polygon)
    {
        var area = 0f;
        for (var i = 0; i < polygon.Count; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % polygon.Count]);
            area += a.X * b.Y - b.X * a.Y;
        }
        if (area < 0) polygon.Reverse();
        return polygon;
    }

    /// <summary>
    /// Signed area per pixel, a row at a time, which a running sum turns into coverage. Each row has
    /// two cells past its width, where the area of an edge right of the bitmap lands and is dropped.
    /// </summary>
    private sealed class Coverage(int width, int height)
    {
        private readonly int _stride = width + 2;
        private readonly float[] _cells = new float[(width + 2) * height];

        /// <summary>Adds a closed polygon, its last point joined back to its first.</summary>
        public void AddPolygon(List<Point> points)
        {
            for (var i = 0; i < points.Count; i++)
                AddEdge(points[i], points[(i + 1) % points.Count]);
        }

        private void AddEdge(Point from, Point to)
        {
            if (from.Y == to.Y) return;
            var direction = 1f;
            var (x0, y0, x1, y1) = (from.X, from.Y, to.X, to.Y);
            if (y0 > y1)
            {
                direction = -1f;
                (x0, y0, x1, y1) = (x1, y1, x0, y0);
            }

            var slope = (x1 - x0) / (y1 - y0);
            var first = Math.Max(0, (int)MathF.Floor(y0));
            var last = Math.Min(height, (int)MathF.Ceiling(y1));
            for (var row = first; row < last; row++)
            {
                var top = MathF.Max(row, y0);
                var bottom = MathF.Min(row + 1, y1);
                if (bottom <= top) continue;
                var xTop = Math.Clamp(x0 + (top - y0) * slope, 0, width);
                var xBottom = Math.Clamp(x0 + (bottom - y0) * slope, 0, width);
                Accumulate(row * _stride, xTop, xBottom, (bottom - top) * direction);
            }
        }

        /// <summary>
        /// One row's share of an edge that crosses it from <paramref name="xa"/> to
        /// <paramref name="xb"/> with signed height <paramref name="d"/>: the area right of the edge
        /// in each pixel it passes, and the rest of <paramref name="d"/> carried to the pixel after.
        /// </summary>
        private void Accumulate(int line, float xa, float xb, float d)
        {
            var (left, right) = xa < xb ? (xa, xb) : (xb, xa);
            var leftCell = (int)MathF.Floor(left);
            var rightCell = (int)MathF.Ceiling(right);
            if (rightCell <= leftCell + 1)
            {
                // Inside one pixel: the part of it right of the edge's mean x is covered.
                var mean = 0.5f * (xa + xb) - leftCell;
                _cells[line + leftCell] += d * (1 - mean);
                _cells[line + leftCell + 1] += d * mean;
                return;
            }

            var reciprocal = 1 / (right - left);
            var leftFraction = left - leftCell;
            var firstArea = 0.5f * reciprocal * (1 - leftFraction) * (1 - leftFraction);
            var rightFraction = right - rightCell + 1;
            var lastArea = 0.5f * reciprocal * rightFraction * rightFraction;
            _cells[line + leftCell] += d * firstArea;
            if (rightCell == leftCell + 2)
            {
                _cells[line + leftCell + 1] += d * (1 - firstArea - lastArea);
            }
            else
            {
                var second = reciprocal * (1.5f - leftFraction);
                _cells[line + leftCell + 1] += d * (second - firstArea);
                for (var cell = leftCell + 2; cell < rightCell - 1; cell++)
                    _cells[line + cell] += d * reciprocal;
                var beforeLast = second + (rightCell - leftCell - 3) * reciprocal;
                _cells[line + rightCell - 1] += d * (1 - beforeLast - lastArea);
            }
            _cells[line + rightCell] += d * lastArea;
        }

        /// <summary>The running sum of each row, clamped to full coverage, as A8.</summary>
        public byte[] ToAlpha()
        {
            var alpha = new byte[width * height];
            for (var row = 0; row < height; row++)
            {
                var sum = 0f;
                for (var column = 0; column < width; column++)
                {
                    sum += _cells[row * _stride + column];
                    alpha[row * width + column] = (byte)MathF.Round(MathF.Min(1, MathF.Abs(sum)) * 255);
                }
            }
            return alpha;
        }
    }
}
