namespace eQuantic.UI.Primitives;

/// <summary>How a shape's fill or stroke is decided.</summary>
public enum VectorPaintKind : byte
{
    /// <summary>Not painted at all — SVG's <c>none</c>, which is how an outline-only shape says it
    /// has no fill.</summary>
    None = 0,

    /// <summary>Painted in whatever tints the drawing — SVG's <c>currentColor</c>. This is the
    /// artwork ASKING to be tinted, and it is what makes a logo follow a theme.</summary>
    Inherit = 1,

    /// <summary>A literal color the artwork chose for itself.</summary>
    Solid = 2,
}

/// <summary>
/// A fill or a stroke. Three states rather than a nullable color, because "no paint" and "the
/// caller's color" are different answers and a null cannot be both.
/// </summary>
public readonly record struct VectorPaint(VectorPaintKind Kind, Color Color)
{
    public static readonly VectorPaint None = new(VectorPaintKind.None, default);

    public static readonly VectorPaint Inherit = new(VectorPaintKind.Inherit, default);

    public static VectorPaint Solid(Color color) => new(VectorPaintKind.Solid, color);

    public bool Paints => Kind != VectorPaintKind.None;

    /// <summary>The color to draw with, given what the drawing is being tinted by. The tint answers
    /// for <see cref="VectorPaintKind.Inherit"/> and for nothing else.</summary>
    public Color Resolve(Color tint) => Kind switch
    {
        VectorPaintKind.Solid => Color,
        VectorPaintKind.Inherit => tint,
        _ => Color.Transparent,
    };
}

/// <summary>
/// One shape of a <see cref="VectorDrawing"/>: path data on the drawing's own grid, plus how it
/// paints. Every SVG shape reduces to this — a <c>&lt;rect&gt;</c> is path data, a
/// <c>&lt;circle&gt;</c> is path data, and a group's transform is BAKED into the data rather than
/// carried, so both realizers draw the same numbers.
/// <para>
/// A shape has at most one fill and one stroke, which is the fence this model draws around SVG:
/// gradients, patterns, masks and filters are not expressible here and are dropped at parse time
/// rather than half-rendered.
/// </para>
/// </summary>
public readonly record struct VectorShape(
    string Path,
    VectorPaint Fill,
    VectorPaint Stroke = default,
    float StrokeWidth = 1,
    bool EvenOdd = false,
    float Opacity = 1);

/// <summary>
/// A piece of vector artwork, target-neutral: a viewBox and the shapes inside it. This is what a
/// <c>.svg</c> FILE becomes — read once at build time, carried as data, drawn by whichever realizer
/// is present.
/// <para>
/// The web lowers it to an inline <c>&lt;svg&gt;</c> with one <c>&lt;path&gt;</c> per shape, so it
/// stays selectable, styleable and tintable in the DOM. Photon rasterizes each shape at the box it
/// was given and paints it in its own color. Neither target reads a file: the browser cannot, and
/// an app that had to ship its artwork twice would drift.
/// </para>
/// <para>
/// Contrast with <see cref="Vector"/>, which is ONE path in ONE tint — an icon. This is the
/// multi-shape, multi-color sibling: a logo, an illustration, a chart legend drawn by a designer.
/// </para>
/// </summary>
public sealed record VectorDrawing(
    float MinX, float MinY, float Width, float Height, IReadOnlyList<VectorShape> Shapes)
{
    /// <summary>The viewBox as an SVG attribute — the web writes it verbatim.</summary>
    public string ViewBox =>
        $"{Number(MinX)} {Number(MinY)} {Number(Width)} {Number(Height)}";

    /// <summary>Width ÷ height of the drawing's own grid, which is what a caller asking for one
    /// dimension needs in order to derive the other without squashing the artwork.</summary>
    public float Aspect => Height <= 0 ? 1 : Width / Height;

    /// <summary>An empty drawing — what an unreadable or unsupported file becomes. It draws
    /// nothing, and drawing nothing is a visible, survivable answer to a bad asset.</summary>
    public static readonly VectorDrawing Empty = new(0, 0, 0, 0, []);

    public bool IsEmpty => Shapes.Count == 0 || Width <= 0 || Height <= 0;

    private static string Number(float value) =>
        MathF.Round(value, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
