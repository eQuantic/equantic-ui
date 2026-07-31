using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Native.Framework;

/// <summary>A normalized path step: everything lowers to moves, lines, cubics and closes.</summary>
public enum PathVerb : byte
{
    Move = 0,
    Line = 1,
    /// <summary>Cubic Bézier — C1/C2 are the control points, End the endpoint.</summary>
    Cubic = 2,
    Close = 3,
}

public readonly record struct PathSegment(PathVerb Verb, Point C1 = default, Point C2 = default, Point End = default);

/// <summary>
/// SVG path-data parser (W4 icons) — pure C#, zero third-party: the ONE normalizer every platform
/// rasterizer consumes. The full command set (M/L/H/V/C/S/Q/T/A/Z, absolute and relative) lowers
/// to moves, lines and CUBICS: quadratics elevate exactly; arcs convert via the SVG F.6.5
/// endpoint→center parameterization split into ≤90° cubic segments. Numbers follow SVG lexing
/// (".5.5", "1-2", compact arc flags). Malformed data returns what parsed so far — icons render
/// best-effort, never throw at draw time.
/// </summary>
public static class SvgPath
{
    public static IReadOnlyList<PathSegment> Parse(string data)
    {
        var segments = new List<PathSegment>();
        var i = 0;
        var command = '\0';
        var current = new Point(0, 0);
        var subpathStart = new Point(0, 0);
        var lastCubicControl = (Point?)null;
        var lastQuadControl = (Point?)null;

        while (i < data.Length)
        {
            SkipSeparators(data, ref i);
            if (i >= data.Length) break;

            var c = data[i];
            if (char.IsLetter(c))
            {
                command = c;
                i++;
                if (command is 'Z' or 'z')
                {
                    segments.Add(new PathSegment(PathVerb.Close));
                    current = subpathStart;
                    lastCubicControl = lastQuadControl = null;
                    continue;
                }
                SkipSeparators(data, ref i);
            }
            else if (command == '\0')
            {
                break; // junk before any command
            }
            else if (command is 'M') command = 'L'; // implicit repeats: M's extras are Ls
            else if (command is 'm') command = 'l';

            var relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                {
                    if (!TryNumber(data, ref i, out var x) || !TryNumber(data, ref i, out var y)) return segments;
                    current = relative ? new Point(current.X + x, current.Y + y) : new Point(x, y);
                    subpathStart = current;
                    segments.Add(new PathSegment(PathVerb.Move, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'L':
                {
                    if (!TryNumber(data, ref i, out var x) || !TryNumber(data, ref i, out var y)) return segments;
                    current = relative ? new Point(current.X + x, current.Y + y) : new Point(x, y);
                    segments.Add(new PathSegment(PathVerb.Line, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'H':
                {
                    if (!TryNumber(data, ref i, out var x)) return segments;
                    current = new Point(relative ? current.X + x : x, current.Y);
                    segments.Add(new PathSegment(PathVerb.Line, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'V':
                {
                    if (!TryNumber(data, ref i, out var y)) return segments;
                    current = new Point(current.X, relative ? current.Y + y : y);
                    segments.Add(new PathSegment(PathVerb.Line, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'C':
                {
                    if (!TryPoint(data, ref i, relative, current, out var c1)
                        || !TryPoint(data, ref i, relative, current, out var c2)
                        || !TryPoint(data, ref i, relative, current, out var end)) return segments;
                    segments.Add(new PathSegment(PathVerb.Cubic, c1, c2, end));
                    lastCubicControl = c2;
                    lastQuadControl = null;
                    current = end;
                    break;
                }
                case 'S':
                {
                    var c1 = Reflect(current, lastCubicControl);
                    if (!TryPoint(data, ref i, relative, current, out var c2)
                        || !TryPoint(data, ref i, relative, current, out var end)) return segments;
                    segments.Add(new PathSegment(PathVerb.Cubic, c1, c2, end));
                    lastCubicControl = c2;
                    lastQuadControl = null;
                    current = end;
                    break;
                }
                case 'Q':
                {
                    if (!TryPoint(data, ref i, relative, current, out var q)
                        || !TryPoint(data, ref i, relative, current, out var end)) return segments;
                    AddQuadratic(segments, current, q, end);
                    lastQuadControl = q;
                    lastCubicControl = null;
                    current = end;
                    break;
                }
                case 'T':
                {
                    var q = Reflect(current, lastQuadControl);
                    if (!TryPoint(data, ref i, relative, current, out var end)) return segments;
                    AddQuadratic(segments, current, q, end);
                    lastQuadControl = q;
                    lastCubicControl = null;
                    current = end;
                    break;
                }
                case 'A':
                {
                    if (!TryNumber(data, ref i, out var rx) || !TryNumber(data, ref i, out var ry)
                        || !TryNumber(data, ref i, out var rotation)
                        || !TryFlag(data, ref i, out var largeArc) || !TryFlag(data, ref i, out var sweep)
                        || !TryPoint(data, ref i, relative, current, out var end)) return segments;
                    AddArc(segments, current, rx, ry, rotation, largeArc, sweep, end);
                    lastCubicControl = lastQuadControl = null;
                    current = end;
                    break;
                }
                default:
                    return segments; // unknown command: keep what parsed
            }
        }
        return segments;
    }

    private static Point Reflect(Point current, Point? control) =>
        control is { } c ? new Point(2 * current.X - c.X, 2 * current.Y - c.Y) : current;

    /// <summary>Exact quadratic→cubic elevation: c1 = p0 + ⅔(q − p0), c2 = end + ⅔(q − end).</summary>
    private static void AddQuadratic(List<PathSegment> segments, Point from, Point q, Point end)
    {
        var c1 = new Point(from.X + 2f / 3 * (q.X - from.X), from.Y + 2f / 3 * (q.Y - from.Y));
        var c2 = new Point(end.X + 2f / 3 * (q.X - end.X), end.Y + 2f / 3 * (q.Y - end.Y));
        segments.Add(new PathSegment(PathVerb.Cubic, c1, c2, end));
    }

    /// <summary>SVG F.6.5 endpoint→center parameterization, split into ≤90° cubic segments.</summary>
    private static void AddArc(List<PathSegment> segments, Point from, float rx, float ry,
        float rotationDegrees, bool largeArc, bool sweep, Point end)
    {
        rx = MathF.Abs(rx);
        ry = MathF.Abs(ry);
        if (rx == 0 || ry == 0 || (from.X == end.X && from.Y == end.Y))
        {
            segments.Add(new PathSegment(PathVerb.Line, End: end));
            return;
        }

        var phi = rotationDegrees * MathF.PI / 180f;
        var (cos, sin) = (MathF.Cos(phi), MathF.Sin(phi));

        // F.6.5.1 — midpoint frame.
        var dx = (from.X - end.X) / 2;
        var dy = (from.Y - end.Y) / 2;
        var x1 = cos * dx + sin * dy;
        var y1 = -sin * dx + cos * dy;

        // F.6.6 — scale radii up if the endpoints cannot be reached.
        var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (lambda > 1)
        {
            var s = MathF.Sqrt(lambda);
            rx *= s;
            ry *= s;
        }

        // F.6.5.2 — center in the rotated frame.
        var sign = largeArc == sweep ? -1f : 1f;
        var num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        var den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        var coefficient = sign * MathF.Sqrt(MathF.Max(0, num / den));
        var cxp = coefficient * (rx * y1 / ry);
        var cyp = coefficient * (-ry * x1 / rx);

        // F.6.5.3 — center back in user space.
        var cx = cos * cxp - sin * cyp + (from.X + end.X) / 2;
        var cy = sin * cxp + cos * cyp + (from.Y + end.Y) / 2;

        // F.6.5.5/6 — start angle and sweep extent.
        var startAngle = Angle(1, 0, (x1 - cxp) / rx, (y1 - cyp) / ry);
        var delta = Angle((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
        if (!sweep && delta > 0) delta -= 2 * MathF.PI;
        else if (sweep && delta < 0) delta += 2 * MathF.PI;

        // Split into ≤90° cubics (k = 4/3 · tan(θ/4)).
        var count = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(delta) / (MathF.PI / 2)));
        var step = delta / count;
        var k = 4f / 3 * MathF.Tan(step / 4);
        var angle = startAngle;
        var p0 = from;
        for (var s = 0; s < count; s++)
        {
            var next = angle + step;
            var (cosA, sinA) = (MathF.Cos(angle), MathF.Sin(angle));
            var (cosB, sinB) = (MathF.Cos(next), MathF.Sin(next));

            Point OnArc(float ca, float sa) => new(
                cx + rx * cos * ca - ry * sin * sa,
                cy + rx * sin * ca + ry * cos * sa);
            Point Derivative(float ca, float sa) => new(
                -rx * cos * sa - ry * sin * ca,
                -rx * sin * sa + ry * cos * ca);

            var p3 = s == count - 1 ? end : OnArc(cosB, sinB);
            var d0 = Derivative(cosA, sinA);
            var d3 = Derivative(cosB, sinB);
            segments.Add(new PathSegment(PathVerb.Cubic,
                new Point(p0.X + k * d0.X, p0.Y + k * d0.Y),
                new Point(p3.X - k * d3.X, p3.Y - k * d3.Y),
                p3));
            p0 = p3;
            angle = next;
        }
    }

    private static float Angle(float ux, float uy, float vx, float vy)
    {
        var dot = ux * vx + uy * vy;
        var len = MathF.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        var angle = MathF.Acos(Math.Clamp(dot / len, -1, 1));
        return (ux * vy - uy * vx) < 0 ? -angle : angle;
    }

    private static bool TryPoint(string data, ref int i, bool relative, Point current, out Point point)
    {
        point = default;
        if (!TryNumber(data, ref i, out var x) || !TryNumber(data, ref i, out var y)) return false;
        point = relative ? new Point(current.X + x, current.Y + y) : new Point(x, y);
        return true;
    }

    /// <summary>Arc flags are a SINGLE 0/1 character (SVG lexing — "0 0 1" may pack as "001").</summary>
    private static bool TryFlag(string data, ref int i, out bool flag)
    {
        SkipSeparators(data, ref i);
        flag = false;
        if (i >= data.Length || (data[i] != '0' && data[i] != '1')) return false;
        flag = data[i] == '1';
        i++;
        return true;
    }

    private static bool TryNumber(string data, ref int i, out float value)
    {
        SkipSeparators(data, ref i);
        var start = i;
        if (i < data.Length && (data[i] == '+' || data[i] == '-')) i++;
        var digits = false;
        while (i < data.Length && char.IsAsciiDigit(data[i])) { i++; digits = true; }
        if (i < data.Length && data[i] == '.')
        {
            i++;
            while (i < data.Length && char.IsAsciiDigit(data[i])) { i++; digits = true; }
        }
        if (digits && i < data.Length && (data[i] == 'e' || data[i] == 'E'))
        {
            var exp = i + 1;
            if (exp < data.Length && (data[exp] == '+' || data[exp] == '-')) exp++;
            if (exp < data.Length && char.IsAsciiDigit(data[exp]))
            {
                i = exp;
                while (i < data.Length && char.IsAsciiDigit(data[i])) i++;
            }
        }
        value = 0;
        return digits && float.TryParse(data.AsSpan(start, i - start),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static void SkipSeparators(string data, ref int i)
    {
        while (i < data.Length && (data[i] == ' ' || data[i] == ',' || data[i] == '\n' || data[i] == '\r' || data[i] == '\t')) i++;
    }
}

/// <summary>
/// The platform icon rasterizer (W4): turns an <see cref="Primitives.IconGlyph"/> into an A8
/// coverage raster at device scale — the Texture command draws it tinted, exactly like text.
/// Null (tests, headless) keeps the deterministic disc placeholder.
/// </summary>
public interface IIconRasterizer
{
    TextRaster? Rasterize(Primitives.IconGlyph glyph, float sizeDp, float scale);
}
