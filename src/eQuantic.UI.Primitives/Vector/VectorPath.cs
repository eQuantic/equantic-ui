namespace eQuantic.UI.Primitives;

/// <summary>A point on a drawing's own grid. Y grows DOWN, the SVG and screen convention.</summary>
public readonly record struct VectorPoint(float X, float Y);

/// <summary>A normalized path step: everything lowers to moves, lines, cubics and closes.</summary>
public enum VectorVerb : byte
{
    Move = 0,
    Line = 1,
    /// <summary>Cubic Bézier — C1/C2 are the control points, End the endpoint.</summary>
    Cubic = 2,
    Close = 3,
}

public readonly record struct VectorSegment(
    VectorVerb Verb, VectorPoint C1 = default, VectorPoint C2 = default, VectorPoint End = default);

/// <summary>
/// A 2D affine transform in SVG's own spelling — <c>matrix(a b c d e f)</c>, applied as
/// <c>x' = a·x + c·y + e</c>, <c>y' = b·x + d·y + f</c>.
/// <para>
/// It exists so that a <c>&lt;g transform&gt;</c> can be FLATTENED into the path data it wraps. The
/// alternative is carrying the matrix to the targets, and the targets do not agree about it: the
/// web has a transform attribute, and the native rasterizer takes a glyph and a box. Baking it here
/// keeps both realizers drawing the same numbers.
/// </para>
/// </summary>
public readonly record struct VectorTransform(float A, float B, float C, float D, float E, float F)
{
    public static readonly VectorTransform Identity = new(1, 0, 0, 1, 0, 0);

    public static VectorTransform Translate(float x, float y) => new(1, 0, 0, 1, x, y);

    public static VectorTransform Scale(float x, float y) => new(x, 0, 0, y, 0, 0);

    /// <summary>Rotation in DEGREES, which is what the attribute is written in.</summary>
    public static VectorTransform Rotate(float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        var (cos, sin) = (MathF.Cos(radians), MathF.Sin(radians));
        return new VectorTransform(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>Skew along X, in degrees.</summary>
    public static VectorTransform SkewX(float degrees) =>
        new(1, 0, MathF.Tan(degrees * MathF.PI / 180f), 1, 0, 0);

    /// <summary>Skew along Y, in degrees.</summary>
    public static VectorTransform SkewY(float degrees) =>
        new(1, MathF.Tan(degrees * MathF.PI / 180f), 0, 1, 0, 0);

    public bool IsIdentity => this == Identity;

    /// <summary>This transform applied AFTER <paramref name="inner"/> — the order nesting reads in:
    /// an outer group's transform composes over the one on the child it contains.</summary>
    public VectorTransform Compose(VectorTransform inner) => new(
        A * inner.A + C * inner.B,
        B * inner.A + D * inner.B,
        A * inner.C + C * inner.D,
        B * inner.C + D * inner.D,
        A * inner.E + C * inner.F + E,
        B * inner.E + D * inner.F + F);

    public VectorPoint Apply(VectorPoint point) =>
        new(A * point.X + C * point.Y + E, B * point.X + D * point.Y + F);
}

/// <summary>
/// SVG path data: the ONE normalizer both targets consume. The full command set
/// (M/L/H/V/C/S/Q/T/A/Z, absolute and relative) lowers to moves, lines and CUBICS — quadratics
/// elevate exactly, and arcs convert through the SVG F.6.5 endpoint→center parameterization split
/// into ≤90° cubic segments. Numbers follow SVG lexing (".5.5", "1-2", compact arc flags).
/// <para>
/// Malformed data returns what parsed so far: a drawing renders best-effort and never throws at
/// draw time, because a figure with one bad subpath is still worth showing.
/// </para>
/// <para>
/// It lives in Primitives, with zero dependencies and no pixels, because BOTH the web realizer and
/// the native rasterizer need the same answer — and because a build-time reader needs it too, to
/// bake a group's transform into the data it wraps.
/// </para>
/// </summary>
public static class VectorPath
{
    public static IReadOnlyList<VectorSegment> Parse(string data)
    {
        var segments = new List<VectorSegment>();
        var i = 0;
        var command = '\0';
        var current = new VectorPoint(0, 0);
        var subpathStart = new VectorPoint(0, 0);
        var lastCubicControl = (VectorPoint?)null;
        var lastQuadControl = (VectorPoint?)null;

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
                    segments.Add(new VectorSegment(VectorVerb.Close));
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
                    current = relative ? new VectorPoint(current.X + x, current.Y + y) : new VectorPoint(x, y);
                    subpathStart = current;
                    segments.Add(new VectorSegment(VectorVerb.Move, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'L':
                {
                    if (!TryNumber(data, ref i, out var x) || !TryNumber(data, ref i, out var y)) return segments;
                    current = relative ? new VectorPoint(current.X + x, current.Y + y) : new VectorPoint(x, y);
                    segments.Add(new VectorSegment(VectorVerb.Line, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'H':
                {
                    if (!TryNumber(data, ref i, out var x)) return segments;
                    current = new VectorPoint(relative ? current.X + x : x, current.Y);
                    segments.Add(new VectorSegment(VectorVerb.Line, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'V':
                {
                    if (!TryNumber(data, ref i, out var y)) return segments;
                    current = new VectorPoint(current.X, relative ? current.Y + y : y);
                    segments.Add(new VectorSegment(VectorVerb.Line, End: current));
                    lastCubicControl = lastQuadControl = null;
                    break;
                }
                case 'C':
                {
                    if (!TryPoint(data, ref i, relative, current, out var c1)
                        || !TryPoint(data, ref i, relative, current, out var c2)
                        || !TryPoint(data, ref i, relative, current, out var end)) return segments;
                    segments.Add(new VectorSegment(VectorVerb.Cubic, c1, c2, end));
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
                    segments.Add(new VectorSegment(VectorVerb.Cubic, c1, c2, end));
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

    /// <summary>
    /// The same shape under <paramref name="transform"/>, as path data again. An identity transform
    /// returns the ORIGINAL string untouched — the common case by far, and re-serializing it would
    /// trade exact author data for rounded numbers to say the same thing.
    /// </summary>
    /// <summary>
    /// The box a path occupies on the drawing's own grid — every point it names, control points
    /// included. Conservative on purpose: a curve stays inside the hull of its control points, and
    /// the alternative (subdividing every cubic to find the tight box) buys a fraction of a percent
    /// for a gradient's placement and costs a solver.
    /// <para>
    /// It exists because SVG's DEFAULT gradient units are fractions of the shape's own box, so a
    /// target that paints the run itself has to know where that box is. The web never asks: the
    /// browser owns the answer there.
    /// </para>
    /// </summary>
    public static (float MinX, float MinY, float MaxX, float MaxY) Bounds(string data)
    {
        var (minX, minY, maxX, maxY) = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        var seen = false;

        void Include(VectorPoint point)
        {
            seen = true;
            minX = MathF.Min(minX, point.X);
            minY = MathF.Min(minY, point.Y);
            maxX = MathF.Max(maxX, point.X);
            maxY = MathF.Max(maxY, point.Y);
        }

        foreach (var segment in Parse(data))
        {
            if (segment.Verb == VectorVerb.Close) continue;
            if (segment.Verb == VectorVerb.Cubic)
            {
                Include(segment.C1);
                Include(segment.C2);
            }
            Include(segment.End);
        }
        return seen ? (minX, minY, maxX, maxY) : (0, 0, 0, 0);
    }

    public static string Transform(string data, VectorTransform transform) =>
        transform.IsIdentity ? data : Serialize(Parse(data), transform);

    /// <summary>Normalized, ABSOLUTE path data: moves, lines, cubics and closes, nothing else.
    /// What a transform flattening emits, and what a test can compare against.</summary>
    public static string Serialize(IReadOnlyList<VectorSegment> segments) =>
        Serialize(segments, VectorTransform.Identity);

    private static string Serialize(IReadOnlyList<VectorSegment> segments, VectorTransform transform)
    {
        var text = new System.Text.StringBuilder();
        foreach (var segment in segments)
        {
            if (text.Length > 0 && segment.Verb != VectorVerb.Close) text.Append(' ');
            switch (segment.Verb)
            {
                case VectorVerb.Move:
                    text.Append('M').Append(Pair(transform.Apply(segment.End)));
                    break;
                case VectorVerb.Line:
                    text.Append('L').Append(Pair(transform.Apply(segment.End)));
                    break;
                case VectorVerb.Cubic:
                    text.Append('C').Append(Pair(transform.Apply(segment.C1)))
                        .Append(' ').Append(Pair(transform.Apply(segment.C2)))
                        .Append(' ').Append(Pair(transform.Apply(segment.End)));
                    break;
                case VectorVerb.Close:
                    text.Append('Z');
                    break;
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// Three decimals, and the shortest spelling of the number that survives them. A drawing's grid
    /// is its viewBox, so three decimals is ~a thousandth of a unit — finer than any device pixel a
    /// 24-unit icon or a 1024-unit logo lands on, and it keeps the emitted data readable.
    /// </summary>
    private static string Pair(VectorPoint point) => $"{Number(point.X)} {Number(point.Y)}";

    private static string Number(float value)
    {
        var rounded = MathF.Round(value, 3);
        if (rounded == 0) rounded = 0; // never "-0"
        return rounded.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static VectorPoint Reflect(VectorPoint current, VectorPoint? control) =>
        control is { } c ? new VectorPoint(2 * current.X - c.X, 2 * current.Y - c.Y) : current;

    /// <summary>Exact quadratic→cubic elevation: c1 = p0 + ⅔(q − p0), c2 = end + ⅔(q − end).</summary>
    private static void AddQuadratic(List<VectorSegment> segments, VectorPoint from, VectorPoint q, VectorPoint end)
    {
        var c1 = new VectorPoint(from.X + 2f / 3 * (q.X - from.X), from.Y + 2f / 3 * (q.Y - from.Y));
        var c2 = new VectorPoint(end.X + 2f / 3 * (q.X - end.X), end.Y + 2f / 3 * (q.Y - end.Y));
        segments.Add(new VectorSegment(VectorVerb.Cubic, c1, c2, end));
    }

    /// <summary>SVG F.6.5 endpoint→center parameterization, split into ≤90° cubic segments.</summary>
    private static void AddArc(List<VectorSegment> segments, VectorPoint from, float rx, float ry,
        float rotationDegrees, bool largeArc, bool sweep, VectorPoint end)
    {
        rx = MathF.Abs(rx);
        ry = MathF.Abs(ry);
        if (rx == 0 || ry == 0 || (from.X == end.X && from.Y == end.Y))
        {
            segments.Add(new VectorSegment(VectorVerb.Line, End: end));
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

            VectorPoint OnArc(float ca, float sa) => new(
                cx + rx * cos * ca - ry * sin * sa,
                cy + rx * sin * ca + ry * cos * sa);
            VectorPoint Derivative(float ca, float sa) => new(
                -rx * cos * sa - ry * sin * ca,
                -rx * sin * sa + ry * cos * ca);

            var p3 = s == count - 1 ? end : OnArc(cosB, sinB);
            var d0 = Derivative(cosA, sinA);
            var d3 = Derivative(cosB, sinB);
            segments.Add(new VectorSegment(VectorVerb.Cubic,
                new VectorPoint(p0.X + k * d0.X, p0.Y + k * d0.Y),
                new VectorPoint(p3.X - k * d3.X, p3.Y - k * d3.Y),
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

    private static bool TryPoint(string data, ref int i, bool relative, VectorPoint current, out VectorPoint point)
    {
        point = default;
        if (!TryNumber(data, ref i, out var x) || !TryNumber(data, ref i, out var y)) return false;
        point = relative ? new VectorPoint(current.X + x, current.Y + y) : new VectorPoint(x, y);
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

    internal static bool TryNumber(string data, ref int i, out float value)
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

    internal static void SkipSeparators(string data, ref int i)
    {
        while (i < data.Length && (data[i] == ' ' || data[i] == ',' || data[i] == '\n' || data[i] == '\r' || data[i] == '\t')) i++;
    }
}
