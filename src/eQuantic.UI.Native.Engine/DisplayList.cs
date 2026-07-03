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
    private Matrix2D _current = Matrix2D.Identity;

    public void Clear(Color color) =>
        _commands.Add(new DrawCommand { Kind = DrawCommandKind.Clear, Paint = Paint.Solid(color) });

    public void FillRect(Rect rect, in Paint paint) => FillRRect(new RRect(rect), paint);

    public void FillRRect(in RRect shape, in Paint paint) => _commands.Add(new DrawCommand
    {
        Kind = DrawCommandKind.FillRRect,
        Shape = shape.Normalized(),
        Paint = paint,
        Transform = _current,
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
        });
    }

    /// <summary>Composes <paramref name="transform"/> ON TOP of the current one (applied first, like nesting).</summary>
    public void PushTransform(in Matrix2D transform)
    {
        _stack.Push(_current);
        _current = transform * _current;
    }

    public void Pop() => _current = _stack.Pop();

    public DisplayList Build()
    {
        if (_stack.Count != 0)
            throw new InvalidOperationException($"Unbalanced transform stack: {_stack.Count} unpopped PushTransform call(s).");
        return new DisplayList(_commands.ToArray());
    }
}
