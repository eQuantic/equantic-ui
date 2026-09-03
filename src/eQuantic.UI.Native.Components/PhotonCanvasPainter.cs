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
    public float Width => box.Width;
    public float Height => box.Height;

    /// <summary>Canvas coordinates are the box's own; the engine's are the window's.</summary>
    private Point At(float x, float y) => new(box.X + x, box.Y + y);

    public void FillRect(float x, float y, float width, float height, ColorToken color, float cornerRadius = 0)
    {
        var origin = At(x, y);
        builder.FillRRect(
            new RRect(new Rect(origin.X, origin.Y, width, height), new CornerRadii(cornerRadius)),
            Paint.Solid(color.Resolve(mode)));
    }

    public void StrokeRect(float x, float y, float width, float height, ColorToken color,
        float strokeWidth, float cornerRadius = 0)
    {
        var origin = At(x, y);
        builder.StrokeRRect(
            new RRect(new Rect(origin.X, origin.Y, width, height), new CornerRadii(cornerRadius)),
            strokeWidth, Paint.Solid(color.Resolve(mode)));
    }

    public void FillCircle(float centerX, float centerY, float radius, ColorToken color) =>
        builder.FillCircle(At(centerX, centerY), radius, Paint.Solid(color.Resolve(mode)));

    public void FillAnnularSector(float centerX, float centerY, float innerRadius, float outerRadius,
        float startAngle, float endAngle, ColorToken color, float cornerSmoothing = 0) =>
        builder.FillAnnularSector(At(centerX, centerY), innerRadius, outerRadius,
            startAngle, endAngle, Paint.Solid(color.Resolve(mode)), cornerSmoothing);

    public void Line(float x1, float y1, float x2, float y2, ColorToken color, float strokeWidth)
    {
        // A line is a thin filled rect, rotated to its own angle — the honest spelling of what an
        // SDF engine draws, and the reason the painter offers no MoveTo/LineTo pair.
        var from = At(x1, y1);
        var to = At(x2, y2);
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
