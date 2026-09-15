using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Photon's <see cref="ICanvasPainter"/>: the app's calls become the engine's own draw commands,
/// offset into the canvas's box.
/// <para>
/// There is no intermediate representation and no path buffer — a <c>FillCircle</c> here IS the
/// engine's <c>FillCircle</c>, so a million-file sunburst is as fast, as antialiased and as
/// correct as the framework's own chrome. That equivalence is why the painter's vocabulary is the
/// display list's and not a general 2D API.
/// </para>
/// </summary>
internal sealed class PhotonCanvasPainter(
    DisplayListBuilder builder, Rect box, ThemeMode mode) : ICanvasPainter
{
    public Size Size => box.Size;

    /// <summary>Canvas coordinates are the box's own; the engine's are the window's.</summary>
    private Point At(Point local) => new(box.X + local.X, box.Y + local.Y);

    public void FillRect(Rect local, ColorToken color, float cornerRadius = 0)
    {
        var origin = At(new Point(local.X, local.Y));
        builder.FillRRect(
            new RRect(new Rect(origin.X, origin.Y, local.Width, local.Height), new CornerRadii(cornerRadius)),
            Paint.Solid(color.Resolve(mode)));
    }

    public void StrokeRect(Rect local, ColorToken color, float strokeWidth, float cornerRadius = 0)
    {
        var origin = At(new Point(local.X, local.Y));
        builder.StrokeRRect(
            new RRect(new Rect(origin.X, origin.Y, local.Width, local.Height), new CornerRadii(cornerRadius)),
            strokeWidth, Paint.Solid(color.Resolve(mode)));
    }

    public void FillCircle(Point center, float radius, ColorToken color) =>
        builder.FillCircle(At(center), radius, Paint.Solid(color.Resolve(mode)));

    public void FillAnnularSector(Point center, float innerRadius, float outerRadius,
        float startAngle, float endAngle, ColorToken color, float cornerSmoothing = 0) =>
        builder.FillAnnularSector(At(center), innerRadius, outerRadius,
            startAngle, endAngle, Paint.Solid(color.Resolve(mode)), cornerSmoothing);

    public void Line(Point start, Point end, ColorToken color, float strokeWidth)
    {
        // A line is a thin filled rect, rotated to its own angle — the honest spelling of what an
        // SDF engine draws, and the reason the painter offers no MoveTo/LineTo pair.
        var from = At(start);
        var to = At(end);
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0) return;

        var midX = (from.X + to.X) / 2;
        var midY = (from.Y + to.Y) / 2;
        var angle = MathF.Atan2(dy, dx);
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);

        // Rotate about the line's midpoint, then draw the rect centred there in local space.
        builder.PushTransform(new Matrix2D(cos, sin, -sin, cos,
            midX - (midX * cos) + (midY * sin), midY - (midX * sin) - (midY * cos)));
        builder.FillRRect(
            new RRect(new Rect(midX - (length / 2), midY - (strokeWidth / 2), length, strokeWidth),
                new CornerRadii(0)),
            Paint.Solid(color.Resolve(mode)));
        builder.Pop();
    }
}
