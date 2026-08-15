using eQuantic.UI.Primitives;
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
        // GROUP-opacity layers (spec S1): BeginLayer redirects drawing into a fresh transparent
        // surface; EndLayer composites it over the one below at the layer's alpha — one blend for
        // the whole group, so overlapping children never double-blend.
        Stack<(ReferenceSurface Below, float Alpha)>? layers = null;
        foreach (ref readonly var command in displayList.Commands)
        {
            switch (command.Kind)
            {
                case DrawCommandKind.Clear:
                    target.Fill(ColorSpace.ToPremultipliedLinear(command.Paint.Color));
                    break;
                case DrawCommandKind.FillRRect:
                case DrawCommandKind.StrokeRRect:
                case DrawCommandKind.ShadowRRect:
                    RasterizeRRect(target, in command);
                    break;
                case DrawCommandKind.Texture:
                    RasterizeTexture(target, in command, displayList.Textures);
                    break;
                case DrawCommandKind.BeginLayer:
                    (layers ??= new()).Push((target, command.StrokeWidth));
                    target = new ReferenceSurface(target.Width, target.Height);
                    break;
                case DrawCommandKind.EndLayer:
                    var (below, alpha) = layers!.Pop();
                    below.CompositeOver(target, alpha);
                    target.Dispose();
                    target = below;
                    break;
                case DrawCommandKind.BackdropBlur:
                    // FENCE (mirrors the GPU): inside a group-opacity layer the backdrop is
                    // isolated — the command is skipped, the glass stays merely translucent.
                    if (layers is { Count: > 0 } || command.Clip is not { } region) break;
                    // Snapshot in the GPU storage format (premultiplied sRGB8): quantizing HERE is
                    // what keeps this pyramid and the GPU pyramid within the parity gate.
                    var snapshot = new BlurImage(target.Width, target.Height);
                    target.ReadPixelsPremultipliedSrgb(snapshot.PremultipliedSrgb);
                    target.CompositeBlurredBackdrop(Blur.Apply(snapshot, command.StrokeWidth), region);
                    break;
            }
        }
    }

    /// <summary>
    /// W4 texture fill (NORMATIVE): the A8 raster samples as COVERAGE with nearest filtering —
    /// rasters are generated at device scale, so texels map 1:1 and stay crisp; the paint color is
    /// the tint. Clip multiplies exactly like the rrect path.
    /// </summary>
    private static void RasterizeTexture(ReferenceSurface target, in DrawCommand command, IReadOnlyList<TextureData> textures)
    {
        if (command.TextureId < 0 || command.TextureId >= textures.Count) return;
        var texture = textures[command.TextureId];
        var rect = command.Shape.Rect;
        var transform = command.Transform;
        var inverse = transform.Invert();
        if (inverse is null || rect.Width <= 0 || rect.Height <= 0) return;

        var bounds = transform.TransformBounds(rect);
        if (command.Clip is { } clipBounds) bounds = bounds.Intersect(clipBounds.Rect.Inflate(1));
        if (bounds.IsEmpty) return;
        var x0 = Math.Max(0, (int)MathF.Floor(bounds.Left));
        var y0 = Math.Max(0, (int)MathF.Floor(bounds.Top));
        var x1 = Math.Min(target.Width, (int)MathF.Ceiling(bounds.Right));
        var y1 = Math.Min(target.Height, (int)MathF.Ceiling(bounds.Bottom));
        if (x1 <= x0 || y1 <= y0) return;

        var inv = inverse.Value;
        // A GRADIENT tint (spec: gradient text) evaluates per pixel through the normative
        // Paint.ColorAt — the same function the SDF path uses, so a gradient over glyph coverage
        // and a gradient over a rrect fill can never disagree. A solid tint hoists out of the loop.
        // ANY run, not just the linear one: artwork brought the radial through the same door
        // gradient text came through, and asking only about linear left a radial-filled shape
        // painted flat in its first stop — a fill that looks deliberate and is not.
        var gradientTint = command.Paint.Kind != PaintKind.Solid;
        var tint = command.Paint.Color;

        for (var py = y0; py < y1; py++)
        {
            for (var px = x0; px < x1; px++)
            {
                var device = new Point(px + 0.5f, py + 0.5f);
                var local = inv.Transform(device);

                var u = (local.X - rect.X) / rect.Width;
                var v = (local.Y - rect.Y) / rect.Height;
                if (u < 0 || u >= 1 || v < 0 || v >= 1) continue;

                var tx = Math.Min(texture.Width - 1, (int)(u * texture.Width));
                var ty = Math.Min(texture.Height - 1, (int)(v * texture.Height));

                float coverage;
                var texel = gradientTint ? command.Paint.ColorAt(local) : tint;
                if (texture.Format == TextureFormat.Rgba8)
                {
                    // W4 images: the texel IS the color (straight sRGB); coverage rides its alpha.
                    var i4 = (ty * texture.Width + tx) * 4;
                    texel = new Color(texture.Alpha[i4], texture.Alpha[i4 + 1], texture.Alpha[i4 + 2], texture.Alpha[i4 + 3]);
                    coverage = 1f;
                    if (texel.A == 0) continue;
                }
                else
                {
                    coverage = texture.Alpha[ty * texture.Width + tx] / 255f;
                    if (coverage <= 0) continue;
                }

                if (command.Clip is { } clip)
                {
                    var clipDistance = Sdf.RoundedRect(
                        device - clip.Rect.Center,
                        new Size(clip.Rect.Width / 2, clip.Rect.Height / 2),
                        clip.Radii);
                    coverage *= Sdf.Coverage(clipDistance);
                    if (coverage <= 0) continue;
                }

                // Same unquantized-alpha rule as the fill path when the tint is a gradient; an RGBA
                // texel carries its own alpha and is untouched.
                if (gradientTint && texture.Format != TextureFormat.Rgba8)
                {
                    target.BlendOver(px, py, ColorSpace.ToPremultipliedLinear(
                        texel with { A = 255 }, coverage * command.Paint.AlphaFactorAt(local)));
                    continue;
                }
                target.BlendOver(px, py, ColorSpace.ToPremultipliedLinear(texel, coverage));
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
        var pad = 1f + command.Kind switch
        {
            DrawCommandKind.StrokeRRect => command.StrokeWidth / 2,
            DrawCommandKind.ShadowRRect => command.StrokeWidth * 1.5f, // the falloff's 1.5σ·2 reach
            _ => 0f,
        };
        var bounds = transform.TransformBounds(shape.Rect.Inflate(pad));
        if (command.Clip is { } clipBounds) bounds = bounds.Intersect(clipBounds.Rect.Inflate(1));
        if (bounds.IsEmpty) return;
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

                var coverage = command.Kind == DrawCommandKind.ShadowRRect
                    ? Sdf.ShadowCoverage(d * scale, command.StrokeWidth * scale)
                    : Sdf.Coverage(d * scale);
                if (coverage <= 0) continue;

                // Clip: multiply by the clip rrect's own coverage (device space, scale 1) — the clip
                // edge anti-aliases exactly like a shape edge.
                if (command.Clip is { } clip)
                {
                    var clipDistance = Sdf.RoundedRect(
                        device - clip.Rect.Center,
                        new Size(clip.Rect.Width / 2, clip.Rect.Height / 2),
                        clip.Radii);
                    coverage *= Sdf.Coverage(clipDistance);
                    if (coverage <= 0) continue;
                }

                // The COLOR comes from the byte ramp (the sRGB→linear LUT is byte-indexed), but the
                // ALPHA rides the coverage scale UNQUANTIZED — at a ramp tail the byte rounding is
                // the entire signal, and dropping it is what made this rasterizer disagree with the
                // shader (which works in float) by 5/255 over high contrast.
                var srgb = command.Paint.ColorAt(local);
                var alpha = command.Paint.AlphaFactorAt(local);
                target.BlendOver(px, py,
                    ColorSpace.ToPremultipliedLinear(srgb with { A = 255 }, coverage * alpha));
            }
        }
    }
}
