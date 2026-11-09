namespace eQuantic.UI.Primitives;

/// <summary>How a shape's fill or stroke is decided.</summary>
public enum VectorPaintKind : byte
{
    /// <summary>Not painted at all — SVG's <c>none</c>, which is how an outline-only shape says it
    /// has no fill.</summary>
    None = 0,

    /// <summary>Painted in whatever tints the drawing — SVG's <c>currentColor</c>. This is the
    /// artwork ASKING to be tinted, and it is what makes a logo follow a theme. Its
    /// <see cref="VectorPaint.Color"/> carries only an ALPHA: a mark whose subtitle is the same ink
    /// at 55% is one drawing, not two.</summary>
    Inherit = 1,

    /// <summary>A literal color the artwork chose for itself.</summary>
    Solid = 2,

    /// <summary>A run between colors along an axis — SVG's <c>linearGradient</c>. The geometry and
    /// the stops live in <see cref="VectorPaint.Gradient"/>.</summary>
    LinearGradient = 3,

    /// <summary>A run outward from a center — SVG's <c>radialGradient</c>.</summary>
    RadialGradient = 4,
}

/// <summary>One stop of a gradient: where it sits along the run (0 to 1) and the color there.</summary>
public readonly record struct VectorStop(float Offset, Color Color);

/// <summary>
/// A gradient paint server, kept in the SVG's OWN terms rather than resolved at parse time: where
/// the run starts and ends is a question about the shape it fills, and the shape's box is not known
/// until a realizer places the drawing.
/// <para>
/// <see cref="UserSpace"/> is the distinction SVG draws with <c>gradientUnits</c>: false (the
/// default, <c>objectBoundingBox</c>) makes the coordinates FRACTIONS of the shape's own box; true
/// (<c>userSpaceOnUse</c>) makes them coordinates on the drawing's viewBox grid. A realizer that
/// collapsed the two would put the run in the right direction and the wrong place.
/// </para>
/// <para>
/// A LINEAR gradient runs from (<see cref="X1"/>, <see cref="Y1"/>) to (<see cref="X2"/>,
/// <see cref="Y2"/>); a RADIAL one is centered there with <see cref="Radius"/>, and ignores the end
/// point. Which one it is comes from the paint's own <see cref="VectorPaintKind"/>, so this carries
/// no second discriminator.
/// </para>
/// </summary>
public readonly record struct VectorGradient(
    float X1,
    float Y1,
    float X2,
    float Y2,
    float Radius,
    bool UserSpace,
    IReadOnlyList<VectorStop> Stops);

/// <summary>
/// A fill or a stroke. Three states rather than a nullable color, because "no paint" and "the
/// caller's color" are different answers and a null cannot be both.
/// </summary>
public readonly record struct VectorPaint(VectorPaintKind Kind, Color Color)
{
    public static readonly VectorPaint None = new(VectorPaintKind.None, default);

    /// <summary>The run this paint is, when it is one. Null for every other kind — the kind is what
    /// says whether to look, and a gradient without stops is not a gradient.</summary>
    public VectorGradient? Gradient { get; init; }

    /// <summary>A run between colors. The kind says linear or radial; the geometry and the stops
    /// say the rest.</summary>
    public static VectorPaint Gradients(VectorPaintKind kind, VectorGradient gradient) =>
        new(kind, gradient.Stops.Count > 0 ? gradient.Stops[0].Color : Color.Transparent)
        {
            Gradient = gradient,
        };

    /// <summary>Tinted at full strength. `Inherit` at a fraction is this with a lower alpha —
    /// <c>new VectorPaint(VectorPaintKind.Inherit, Color.Black.WithOpacity(0.55f))</c>, where only
    /// the alpha is read.</summary>
    public static readonly VectorPaint Inherit = new(VectorPaintKind.Inherit, Color.Black);

    public static VectorPaint Solid(Color color) => new(VectorPaintKind.Solid, color);

    public bool Paints => Kind != VectorPaintKind.None;

    /// <summary>The color to draw with, given what the drawing is being tinted by. The tint answers
    /// for <see cref="VectorPaintKind.Inherit"/> and for nothing else.</summary>
    public Color Resolve(Color tint) => Kind switch
    {
        VectorPaintKind.Solid => Color,
        // A gradient asked for as ONE colour is its first stop: the answer a target gives when it
        // can only fill flat, and the one every fallback in both realizers already spells.
        VectorPaintKind.LinearGradient or VectorPaintKind.RadialGradient => Color,
        // The tint answers WHICH colour; the paint still owns how much of it — `fill-opacity` on a
        // `currentColor` shape would otherwise be dropped, and a subtitle drawn at full strength
        // is a different logo.
        VectorPaintKind.Inherit => tint.WithOpacity(Color.A / 255f),
        _ => Color.Transparent,
    };

    /// <summary>The alpha this paint applies on top of whatever colour it resolves to.</summary>
    public float Alpha => Kind == VectorPaintKind.None ? 0 : Color.A / 255f;

    /// <summary>Whether this paint is a run of colors rather than one — what a realizer branches on
    /// before it reads <see cref="Gradient"/>.</summary>
    public bool IsGradient =>
        Kind is VectorPaintKind.LinearGradient or VectorPaintKind.RadialGradient && Gradient is not null;
}

/// <summary>
/// One shape of a <see cref="VectorDrawing"/>: path data on the drawing's own grid, plus how it
/// paints. Every SVG shape reduces to this — a <c>&lt;rect&gt;</c> is path data, a
/// <c>&lt;circle&gt;</c> is path data, and a group's transform is BAKED into the data rather than
/// carried, so both realizers draw the same numbers.
/// <para>
/// A shape has at most one fill and one stroke, each of them flat, tinted, or a GRADIENT. Patterns,
/// masks and filters are still not expressible here and are dropped at parse time rather than
/// half-rendered.
/// </para>
/// <para>
/// A shape with <see cref="OnPressed"/> is an INPUT as well as a picture: a concelho on a map, a
/// seat on a plan. It takes a press where its path is painted (its fill rule decides what is inside,
/// never its bounding box), it is a stop of the keyboard's focus order and answers Enter and Space,
/// it shows <see cref="Hover"/> under the pointer, and it is announced by <see cref="Label"/> with
/// <see cref="Selected"/> as its state. It is one input per shape, laid on the drawing's own grid, so
/// it scales with the drawing where a <see cref="Pressable"/> laid over it would not. Flutter reaches
/// the same with a <c>CustomPainter</c> whose <c>hitTest</c> asks <c>Path.contains</c> and whose
/// <c>semanticsBuilder</c> names each region (#674).
/// </para>
/// </summary>
public readonly record struct VectorShape(
    string Path,
    VectorPaint Fill,
    VectorPaint Stroke = default,
    float StrokeWidth = 1,
    bool EvenOdd = false,
    float Opacity = 1)
{
    private readonly StyleDiff? _hover;

    /// <summary>What assistive tech announces for the shape: "Matosinhos". A shape that takes a press
    /// and has no label is announced by nothing a person can tell apart from its neighbours, so give
    /// every input one.</summary>
    public string? Label { get; init; }

    /// <summary>Called when the shape is pressed, by a pointer inside its path or by Enter or Space
    /// while it has the focus. Null keeps the shape a picture: no press, no focus, no hover.</summary>
    public Action? OnPressed { get; init; }

    /// <summary>The shape's toggle state, announced as pressed or not: the concelho a map picked. Null
    /// states none. It is a state, not a paint: the fill says what the person sees, and this says
    /// what assistive tech hears, as <see cref="Pressable.Selected"/> does.</summary>
    public bool? Selected { get; init; }

    /// <summary>
    /// The paint while the pointer is over the shape, laid over its own. A shape is not a box, so a
    /// diff reaches it through the members that mean something on a path: <see cref="StyleDiff.Background"/>
    /// is its fill, <see cref="StyleDiff.BorderColor"/> and <see cref="StyleDiff.BorderWidth"/> its
    /// stroke, and <see cref="StyleDiff.Opacity"/> its opacity. A diff that sets any other member is
    /// refused here, because a path has no elevation, gradient layer, backdrop, transform or shadow
    /// list to apply it to, and a member silently ignored is a hover that looks right on nobody's
    /// screen.
    /// </summary>
    public StyleDiff? Hover
    {
        get => _hover;
        init
        {
            if (value is { } diff && (diff.Elevation is not null || diff.Gradient is not null
                || diff.BackdropBlur is not null || diff.Transform is not null || diff.Shadows is not null))
                throw new ArgumentException(
                    "A shape's hover can change its fill (Background), its stroke (BorderColor, BorderWidth) "
                    + "and its opacity, and nothing else: a path has no elevation, gradient layer, backdrop, "
                    + "transform or shadow list.", nameof(Hover));
            _hover = value;
        }
    }

    /// <summary>Whether the shape takes a press, and with it the focus and the hover.</summary>
    public bool IsInput => OnPressed is not null;
}

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

    /// <summary>Four decimals, which is what the web's own number formatter uses — the viewBox is
    /// emitted by BOTH lowerings and a digit of disagreement is a patch on every hydration.</summary>
    private static string Number(float value) =>
        value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}
