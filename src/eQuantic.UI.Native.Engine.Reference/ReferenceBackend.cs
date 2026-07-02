namespace eQuantic.UI.Native.Engine.Reference;

/// <summary>
/// The CPU reference backend (plan D7): a deliberately simple, scalar, per-pixel rasterizer that
/// evaluates the engine's normative <see cref="Sdf"/> math directly. It exists so golden-image tests
/// have a ground truth and GPU results can be bisected into "engine bug vs driver bug". It is test
/// infrastructure — clarity beats speed everywhere in this project.
/// </summary>
public sealed class ReferenceBackend : IRenderBackend
{
    public RenderBackendKind Kind => RenderBackendKind.Reference;

    public IRenderSurface CreateSurface(int width, int height) => new ReferenceSurface(width, height);

    public void Dispose()
    {
        // No device resources on the CPU; GPU backends release their device/queues here.
    }

    public void Render(DisplayList displayList, IRenderSurface surface)
    {
        var target = (ReferenceSurface)surface;
        foreach (ref readonly var command in displayList.Commands)
        {
            switch (command.Kind)
            {
                case DrawCommandKind.Clear:
                    target.Fill(ColorSpace.ToPremultipliedLinear(command.Paint.Color));
                    break;
                case DrawCommandKind.FillRRect:
                case DrawCommandKind.StrokeRRect:
                    RasterizeRRect(target, in command);
                    break;
            }
        }
    }

    private static void RasterizeRRect(ReferenceSurface target, in DrawCommand command)
    {
        var shape = command.Shape;
        var transform = command.Transform;
        var inverse = transform.Invert();
        if (inverse is null) return; // degenerate transform: zero-area shape, nothing to draw

        // Device-space AABB of the (transformed) shape, padded for the AA ramp and stroke band.
        var pad = 1f + (command.Kind == DrawCommandKind.StrokeRRect ? command.StrokeWidth / 2 : 0);
        var bounds = transform.TransformBounds(shape.Rect.Inflate(pad));
        var x0 = Math.Max(0, (int)MathF.Floor(bounds.Left));
        var y0 = Math.Max(0, (int)MathF.Floor(bounds.Top));
        var x1 = Math.Min(target.Width, (int)MathF.Ceiling(bounds.Right));
        var y1 = Math.Min(target.Height, (int)MathF.Ceiling(bounds.Bottom));
        if (x1 <= x0 || y1 <= y0) return;

        var center = shape.Rect.Center;
        var halfSize = new Size(shape.Rect.Width / 2, shape.Rect.Height / 2);
        // Local SDF distances scale to device pixels by the transform's average scale (see
        // Matrix2D.AverageScale for the non-uniform-scale caveat vs the shader's fwidth).
        var scale = transform.AverageScale();
        if (scale <= 0) return;
        var inv = inverse.Value;

        for (var py = y0; py < y1; py++)
        {
            for (var px = x0; px < x1; px++)
            {
                var device = new Point(px + 0.5f, py + 0.5f);
                var local = inv.Transform(device);

                var d = Sdf.RoundedRect(local - center, halfSize, shape.Radii);
                if (command.Kind == DrawCommandKind.StrokeRRect)
                    d = Sdf.Stroke(d, command.StrokeWidth);

                var coverage = Sdf.Coverage(d * scale);
                if (coverage <= 0) continue;

                var srgb = command.Paint.ColorAt(local);
                target.BlendOver(px, py, ColorSpace.ToPremultipliedLinear(srgb, coverage));
            }
        }
    }
}
