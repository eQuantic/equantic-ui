using eQuantic.UI.Primitives;
namespace eQuantic.UI.Native.Engine;

public enum DrawCommandKind : byte
{
    /// <summary>Fill the whole surface with <see cref="DrawCommand.Paint"/>'s color (no blending).</summary>
    Clear = 0,

    /// <summary>Fill a rounded rectangle (radius 0 = rect; radius = half-size = circle/pill).</summary>
    FillRRect = 1,

    /// <summary>Stroke a rounded rectangle's edge with a centered band of <see cref="DrawCommand.StrokeWidth"/>.</summary>
    StrokeRRect = 2,

    /// <summary>The §05 analytic rrect shadow — <see cref="DrawCommand.StrokeWidth"/> carries the BLUR;
    /// offset/spread are baked into <see cref="DrawCommand.Shape"/> by the builder.</summary>
    ShadowRRect = 3,

    /// <summary>Begin a GROUP-opacity layer (spec S1): commands until the matching
    /// <see cref="EndLayer"/> composite as ONE surface at <see cref="DrawCommand.StrokeWidth"/>
    /// (the layer ALPHA riding the scalar slot, like blur does for shadows).</summary>
    BeginLayer = 4,

    /// <summary>Composite the current layer onto the surface below at its alpha.</summary>
    EndLayer = 5,
}

/// <summary>
/// One flat, heap-free draw command. <see cref="Shape"/> is in LOCAL space; <see cref="Transform"/>
/// maps local → device (baked by the builder from its transform stack, so rasterizers never track
/// state). Radii are pre-normalized by the builder.
/// </summary>
public readonly record struct DrawCommand
{
    public DrawCommandKind Kind { get; init; }
    public RRect Shape { get; init; }
    public Paint Paint { get; init; }
    public float StrokeWidth { get; init; }
    public Matrix2D Transform { get; init; }

    /// <summary>DEVICE-space rounded-rect clip baked by the builder (like the transform), or null =
    /// unclipped. Rasterizers multiply pixel coverage by the clip's SDF coverage — anti-aliased clip
    /// edges by construction.</summary>
    public RRect? Clip { get; init; }
}

/// <summary>
/// An immutable recorded frame: the flat sequence of draw commands a backend consumes. v1 wraps an
/// array; the arena/struct-of-arrays layout (plan W3) replaces the storage without changing consumers.
/// </summary>
public sealed class DisplayList
{
    private readonly DrawCommand[] _commands;

    internal DisplayList(DrawCommand[] commands) => _commands = commands;

    public ReadOnlySpan<DrawCommand> Commands => _commands;
    public int Count => _commands.Length;
}

/// <summary>
/// Records draw commands with a transform stack (<see cref="PushTransform"/>/<see cref="Pop"/>);
/// the current transform is BAKED into each command. Shapes are radius-normalized on record.
/// </summary>
public sealed class DisplayListBuilder
{
    private readonly List<DrawCommand> _commands = new();
    private readonly Stack<Matrix2D> _stack = new();
    private readonly Stack<RRect?> _clipStack = new();
    private Matrix2D _current = Matrix2D.Identity;
    private RRect? _clip;

    public void Clear(Color color) =>
        _commands.Add(new DrawCommand { Kind = DrawCommandKind.Clear, Paint = Paint.Solid(color) });

    public void FillRect(Rect rect, in Paint paint) => FillRRect(new RRect(rect), paint);

    public void FillRRect(in RRect shape, in Paint paint) => _commands.Add(new DrawCommand
    {
        Kind = DrawCommandKind.FillRRect,
        Shape = shape.Normalized(),
        Paint = paint,
        Transform = _current,
        Clip = _clip,
    });

    public void StrokeRRect(in RRect shape, float strokeWidth, in Paint paint)
    {
        if (strokeWidth <= 0) return;
        _commands.Add(new DrawCommand
        {
            Kind = DrawCommandKind.StrokeRRect,
            Shape = shape.Normalized(),
            Paint = paint,
            StrokeWidth = strokeWidth,
            Transform = _current,
            Clip = _clip,
        });
    }

    /// <summary>
    /// Records the §05 analytic shadow for <paramref name="shape"/>: the shadow shape is the rrect
    /// offset by <paramref name="offsetY"/> and inflated by <paramref name="spread"/> (baked here);
    /// <paramref name="blur"/> rides the StrokeWidth slot. Draw it BEFORE the shape's fill.
    /// </summary>
    public void ShadowRRect(in RRect shape, float offsetY, float blur, float spread, Color color)
    {
        var rect = new Rect(shape.Rect.X, shape.Rect.Y + offsetY, shape.Rect.Width, shape.Rect.Height)
            .Inflate(spread);
        var radii = new CornerRadii(
            shape.Radii.TopLeft + spread, shape.Radii.TopRight + spread,
            shape.Radii.BottomRight + spread, shape.Radii.BottomLeft + spread);
        _commands.Add(new DrawCommand
        {
            Kind = DrawCommandKind.ShadowRRect,
            Shape = new RRect(rect, radii).Normalized(),
            Paint = Paint.Solid(color),
            StrokeWidth = blur,
            Transform = _current,
            Clip = _clip,
        });
    }

    /// <summary>Composes <paramref name="transform"/> ON TOP of the current one (applied first, like nesting).</summary>
    public void PushTransform(in Matrix2D transform)
    {
        _stack.Push(_current);
        _current = transform * _current;
    }

    public void Pop() => _current = _stack.Pop();

    private int _openLayers;

    /// <summary>
    /// Begins a GROUP-opacity layer: everything until <see cref="PopLayer"/> composites as one
    /// surface at <paramref name="alpha"/> — overlapping children inside never double-blend
    /// (the CSS <c>opacity</c> semantics). Alpha is clamped to 0–1.
    /// </summary>
    public void PushLayer(float alpha)
    {
        _openLayers++;
        _commands.Add(new DrawCommand
        {
            Kind = DrawCommandKind.BeginLayer,
            StrokeWidth = Math.Clamp(alpha, 0f, 1f),
        });
    }

    public void PopLayer()
    {
        _openLayers--;
        _commands.Add(new DrawCommand { Kind = DrawCommandKind.EndLayer });
    }

    /// <summary>
    /// Pushes a rounded-rect clip, given in the CURRENT transform's local space and baked to device
    /// space (v1 fence: the baked rect is the transform's AABB of the shape — exact under
    /// translation/scale, approximate under rotation). Nested clips intersect their RECTS (AABB) and
    /// keep the INNERMOST radii — exact for the dominant case (nested scroll viewports); fully
    /// general rrect∩rrect joins with a real clip-stack primitive if ever needed.
    /// </summary>
    public void PushClip(in RRect shape)
    {
        _clipStack.Push(_clip);
        var normalized = shape.Normalized();
        var deviceRect = _current.TransformBounds(normalized.Rect);
        var scale = _current.AverageScale();
        var radii = new CornerRadii(
            normalized.Radii.TopLeft * scale, normalized.Radii.TopRight * scale,
            normalized.Radii.BottomRight * scale, normalized.Radii.BottomLeft * scale);
        var device = new RRect(deviceRect, radii).Normalized();

        _clip = _clip is { } outer
            ? new RRect(outer.Rect.Intersect(device.Rect), device.Radii).Normalized()
            : device;
    }

    public void PopClip() => _clip = _clipStack.Pop();

    public DisplayList Build()
    {
        if (_stack.Count != 0)
            throw new InvalidOperationException($"Unbalanced transform stack: {_stack.Count} unpopped PushTransform call(s).");
        if (_clipStack.Count != 0)
            throw new InvalidOperationException($"Unbalanced clip stack: {_clipStack.Count} unpopped PushClip call(s).");
        if (_openLayers != 0)
            throw new InvalidOperationException($"Unbalanced layer stack: {_openLayers} unpopped PushLayer call(s).");
        return new DisplayList(_commands.ToArray());
    }
}
