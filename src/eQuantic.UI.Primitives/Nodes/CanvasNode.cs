namespace eQuantic.UI.Primitives;

/// <summary>
/// The shapes a <see cref="Canvas"/> can draw, in the coordinates of its own box — (0,0) is the
/// canvas's top-left corner, whatever the layout decided that is.
/// <para>
/// The vocabulary is deliberately the ENGINE's, not a general 2D API: rounded rectangles, circles,
/// annular sectors, strokes. There are no arbitrary paths here and there will not be — Photon is an
/// SDF rasterizer, not a canvas library, and a path engine is a normative v2 fence in the engine
/// plan. What this buys is that everything drawn through it is exactly as fast, as antialiased and
/// as correct as everything the framework draws for itself, on every target.
/// </para>
/// <para>
/// A painter is handed to the draw callback for ONE frame and must not be kept: it is the frame
/// being built, and calls made after it returns belong to nothing.
/// </para>
/// </summary>
public interface ICanvasPainter
{
    /// <summary>The box this canvas was given — width and height in device-independent points.</summary>
    float Width { get; }

    /// <summary>The box's height.</summary>
    float Height { get; }

    /// <summary>A filled rectangle, optionally rounded.</summary>
    void FillRect(float x, float y, float width, float height, ColorToken color, float cornerRadius = 0);

    /// <summary>A rectangle's outline, drawn inside its bounds like every border in the framework.</summary>
    void StrokeRect(float x, float y, float width, float height, ColorToken color, float strokeWidth,
        float cornerRadius = 0);

    /// <summary>A filled circle.</summary>
    void FillCircle(float centerX, float centerY, float radius, ColorToken color);

    /// <summary>
    /// A filled ring segment — the shape a sunburst, a donut gauge or a ring selection is made of.
    /// Angles are in RADIANS, measured clockwise from three o'clock, the convention the engine's own
    /// sector uses. <paramref name="cornerSmoothing"/> rounds the four corners of the segment.
    /// <para>
    /// DEGENERATE INPUT DRAWS NOTHING, and the rules are the engine's own so both targets agree: a
    /// non-positive outer radius, an end angle at or before the start (sectors sweep clockwise, and
    /// a reversed one is a mistake rather than a counter-clockwise wish), or an inner radius that
    /// reaches the outer. What is merely out of range is CLAMPED instead: a sweep past a full turn
    /// stops at one, and smoothing beyond half the band's width stops there — past it the inset
    /// shape inverts.
    /// </para>
    /// </summary>
    void FillAnnularSector(float centerX, float centerY, float innerRadius, float outerRadius,
        float startAngle, float endAngle, ColorToken color, float cornerSmoothing = 0);

    /// <summary>A straight line, as a stroked rectangle — the honest spelling of what the engine
    /// draws, and the reason there is no <c>MoveTo</c>/<c>LineTo</c> pair here.</summary>
    void Line(float x1, float y1, float x2, float y2, ColorToken color, float strokeWidth);
}

/// <summary>
/// A component draws its OWN pixels here, inside a box the layout gives it, once per frame.
/// <para>
/// Everything else in the vocabulary is composed from nodes, which is right for a user interface
/// and wrong for a visualization: a sunburst of a million files, a physics-driven set of bubbles, a
/// chart whose geometry is recomputed from data that never stops. Those are ARITHMETIC over a box,
/// and expressing them as thousands of retained nodes costs a reconciler pass to say what a loop
/// could say directly.
/// </para>
/// <para>
/// Pointer events arrive in the canvas's OWN coordinates, which is what makes the arithmetic the
/// app's rather than the engine's: polar hit-testing for a sunburst, per-particle picking for a
/// simulation, a hover that reads its own geometry. The engine does not know what was drawn and
/// does not try to — it hands over the point, and the app decides what is under it.
/// </para>
/// <para>
/// FENCE, stated where it is felt: only the engine's shapes (see <see cref="ICanvasPainter"/>). A
/// canvas is not an escape hatch into arbitrary drawing; it is the same nine commands, addressed
/// directly.
/// </para>
/// </summary>
public sealed class Canvas : VisualNode
{
    /// <param name="draw">Called once per frame with a painter over this canvas's box.</param>
    /// <param name="width">The box's width; defaults to filling the space offered.</param>
    /// <param name="height">The box's height.</param>
    public Canvas(Action<ICanvasPainter> draw, SizeValue width = default, SizeValue height = default)
    {
        Draw = draw;
        Width = width.Kind == SizeKind.Hug ? SizeValue.Fill : width;
        Height = height.Kind == SizeKind.Hug ? SizeValue.Fill : height;
    }

    public sealed override string NodeKind => "canvas";

    /// <summary>How wide a box the layout should give it. Defaults to Fill: a canvas with no stated
    /// size wants the room it is offered, which is what a visualization almost always means.</summary>
    public SizeValue Width { get; init; }

    /// <summary>How tall a box to give it; Fill by default, for the same reason.</summary>
    public SizeValue Height { get; init; }

    /// <summary>The per-frame painter callback.</summary>
    public Action<ICanvasPainter> Draw { get; }

    /// <summary>
    /// A pointer press inside the canvas, in ITS coordinates. The app decides what was hit — the
    /// engine knows only that the point landed in the box.
    /// </summary>
    public Action<CanvasPointer>? OnPointerDown { get; init; }

    /// <summary>The pointer moving inside the canvas, pressed or not — <see cref="CanvasPointer.Pressed"/>
    /// says which, so one handler serves both hovering and dragging.</summary>
    public Action<CanvasPointer>? OnPointerMove { get; init; }

    /// <summary>The press ending inside the canvas.</summary>
    public Action<CanvasPointer>? OnPointerUp { get; init; }

    /// <summary>The pointer leaving the canvas — the moment a hover highlight must be cleared, and
    /// the one an app cannot infer from moves that simply stop arriving.</summary>
    public Action? OnPointerLeave { get; init; }

    /// <summary>What assistive technology is told this canvas IS, because a drawing says nothing on
    /// its own. Null leaves it decorative, which is the honest answer for pure ornament.</summary>
    public string? Label { get; init; }
}

/// <summary>A pointer event in a canvas's own coordinates.</summary>
/// <param name="X">Distance from the canvas's left edge, in points.</param>
/// <param name="Y">Distance from its top edge.</param>
/// <param name="Pressed">Whether a button is down — what separates a drag from a hover.</param>
/// <param name="Modifiers">The modifier keys held, for the app's own gestures.</param>
public readonly record struct CanvasPointer(float X, float Y, bool Pressed, KeyModifiers Modifiers)
{
    /// <summary>The angle from a centre, in radians clockwise from three o'clock — the same
    /// convention <see cref="ICanvasPainter.FillAnnularSector"/> uses, so a sunburst's hit test is
    /// a comparison rather than a conversion.</summary>
    public float AngleFrom(float centerX, float centerY) =>
        MathF.Atan2(Y - centerY, X - centerX);

    /// <summary>The distance from a centre — the other half of a polar hit test.</summary>
    public float DistanceFrom(float centerX, float centerY)
    {
        var dx = X - centerX;
        var dy = Y - centerY;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
