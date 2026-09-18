using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Pixels that come from somewhere else — a decoded bitmap, a camera session, a rasterized
/// glyph or vector — plus the two nodes that draw themselves (a canvas and a spinner).
/// </summary>
internal sealed partial class EmitVisitor
{
    // Spec A11 fence: a SurfaceSubtle box under the radius stands in for the bitmap until the
    // engine gains texture upload (M4) - the documented placeholder pattern.
    private void EmitImageNode(Image image, EmitState s)
    {
        EmitImage(s.Node, image, s.Theme, s.Mode, s.Builder, s.Motion);
    }

    private void EmitCamera(CameraPreview camera, EmitState s)
    {
        EmitCameraPreview(s.Node, camera, s.Theme, s.Mode, s.Builder, s.Motion);
    }

    // Spec A10, W4 fence: a tinted disc at 30% alpha stands in for the glyph until the atlas
    // lands — the same documented placeholder pattern as text bars.
    private void EmitIcon(Icon icon, EmitState s)
    {
        // W4: REAL glyph when the platform service is present — one A8 raster per
        // (glyph, size, scale), drawn as a tinted Texture command. No service → the
        // documented 30% disc placeholder (tests, headless).
        if (s.Motion.IconRasterizer is { } icons
            && (s.Motion.IconCache ?? IconRasterCache.Shared)
                .Get(icons, icon.Glyph, icon.Size, icon.Size, s.Motion.RenderScale) is { } raster)
        {
            var tint = (icon.Color ?? s.Theme.TextPrimary).Resolve(s.Mode);
            s.Builder.Texture(s.Node.Bounds, tint, raster);
            return;
        }
        var placeholder = (icon.Color ?? s.Theme.TextPrimary).Resolve(s.Mode).WithOpacity(0.30f);
        s.Builder.FillRRect(
            new RRect(s.Node.Bounds, new CornerRadii(s.Node.Bounds.Width / 2)),
            Paint.Solid(placeholder));
    }

    private void EmitVector(Vector vector, EmitState s)
    {
        // The vector rides the SAME rasterizer as an icon — one tinted A8 raster per
        // (glyph, box, scale), the box being whatever aspect the author asked for. No
        // service → the icon placeholder disc, same contract.
        if (s.Motion.IconRasterizer is { } vectorIcons
            && (s.Motion.IconCache ?? IconRasterCache.Shared)
                .Get(vectorIcons, vector.Glyph, vector.Size, vector.Height, s.Motion.RenderScale)
                is { } vectorRaster)
        {
            var vectorTint = (vector.Color ?? s.Theme.TextPrimary).Resolve(s.Mode);
            s.Builder.Texture(s.Node.Bounds, vectorTint, vectorRaster);
            return;
        }
        var vectorPlaceholder = (vector.Color ?? s.Theme.TextPrimary).Resolve(s.Mode).WithOpacity(0.30f);
        s.Builder.FillRRect(
            new RRect(s.Node.Bounds, new CornerRadii(s.Node.Bounds.Width / 2)),
            Paint.Solid(vectorPlaceholder));
    }

    // Artwork: the same rasterizer an icon rides, ONCE PER SHAPE, each drawn in its own
    // colour. The alternative was teaching the engine about colour rasters, and this needs
    // nothing new: a texture command already carries a tint, and a drawing is a handful of
    // shapes, not a scene.
    private void EmitDrawing(Drawing drawing, EmitState s)
    {
        if (s.Motion.IconRasterizer is not { } drawingIcons)
        {
            // No platform service (tests, headless): the documented placeholder, one box
            // rather than one per shape — it stands for the artwork, not for its parts.
            var box = (drawing.Tint ?? s.Theme.TextPrimary).Resolve(s.Mode).WithOpacity(0.30f);
            s.Builder.FillRRect(new RRect(s.Node.Bounds, new CornerRadii(0)), Paint.Solid(box));
            return;
        }

        var cache = s.Motion.IconCache ?? IconRasterCache.Shared;
        var inherited = (drawing.Tint ?? s.Theme.TextPrimary).Resolve(s.Mode);
        foreach (var shape in drawing.Artwork.Shapes)
        {
            // Fill and stroke are two rasters, because they are two shapes: the same path
            // as an alpha mask, and the same path as an outline of a given width.
            if (shape.Fill.Paints)
                EmitShape(shape, IconGlyphStyle.Fill, shape.Fill);
            if (shape.Stroke.Paints)
                EmitShape(shape, IconGlyphStyle.Stroke, shape.Stroke);
        }

        void EmitShape(VectorShape shape, IconGlyphStyle style, VectorPaint paint)
        {
            var glyph = new IconGlyph("", shape.Path, style, drawing.Artwork.ViewBox,
                shape.StrokeWidth);
            if (cache.Get(drawingIcons, glyph, drawing.Width, drawing.Height,
                    s.Motion.RenderScale) is not { } raster) return;
            s.Builder.Texture(s.Node.Bounds, RunPaint(shape, paint, inherited), raster);
        }

        // The mask, filled. A flat paint tints the coverage; a RUN fills it with the same
        // two-stop gradient the engine already paints text with, so artwork and type go
        // through one path instead of two that agree by inspection.
        Paint RunPaint(VectorShape shape, VectorPaint paint, Color tint)
        {
            var flat = Paint.Solid(paint.Resolve(tint).WithOpacity(shape.Opacity));
            if (!paint.IsGradient) return flat;

            var run = paint.Gradient!.Value;
            if (run.Stops.Count == 0) return flat;
            // Two stops, first and last. The engine's paint interpolates between a pair —
            // multi-stop is its own slice (a stop pool the shader indexes), and until then
            // a three-stop run arrives as its ends rather than as nothing.
            var from = run.Stops[0].Color.WithOpacity(shape.Opacity * run.Stops[0].Color.A / 255f);
            var last = run.Stops[^1].Color;
            var to = last.WithOpacity(shape.Opacity * last.A / 255f);

            var box = Frame(shape, run, s.Node.Bounds, drawing.Artwork);
            return paint.Kind == VectorPaintKind.RadialGradient
                ? Paint.Radial(
                    new Point(box.X + run.X1 * box.Width, box.Y + run.Y1 * box.Height),
                    run.Radius * box.Width, run.Radius * box.Height, from, to)
                : Paint.Linear(
                    new Point(box.X + run.X1 * box.Width, box.Y + run.Y1 * box.Height),
                    new Point(box.X + run.X2 * box.Width, box.Y + run.Y2 * box.Height),
                    from, to);
        }

        // Where the run's fractions are measured from. SVG's default is the SHAPE's own box
        // (objectBoundingBox), which is why the path has to answer for its own bounds;
        // userSpaceOnUse measures on the viewBox grid, so the whole drawing's box is the
        // frame and the coordinates arrive already divided by it.
        static Rect Frame(VectorShape shape, VectorGradient run, Rect bounds, VectorDrawing artwork)
        {
            if (run.UserSpace)
            {
                var scaleX = artwork.Width > 0 ? bounds.Width / artwork.Width : 1;
                var scaleY = artwork.Height > 0 ? bounds.Height / artwork.Height : 1;
                return new Rect(
                    bounds.X - artwork.MinX * scaleX, bounds.Y - artwork.MinY * scaleY,
                    scaleX, scaleY);
            }

            var (minX, minY, maxX, maxY) = VectorPath.Bounds(shape.Path);
            var unitX = artwork.Width > 0 ? bounds.Width / artwork.Width : 1;
            var unitY = artwork.Height > 0 ? bounds.Height / artwork.Height : 1;
            return new Rect(
                bounds.X + (minX - artwork.MinX) * unitX,
                bounds.Y + (minY - artwork.MinY) * unitY,
                MathF.Max(maxX - minX, 0) * unitX,
                MathF.Max(maxY - minY, 0) * unitY);
        }
    }

    private void EmitCanvas(Canvas canvas, EmitState s)
    {
        // The app draws its own pixels here, in ITS coordinates, once per frame. The
        // painter is handed out for THIS frame only — it is the frame being built.
        canvas.Draw(new PhotonCanvasPainter(s.Builder, s.Node.Bounds, s.Mode));
        if (canvas.OnPointerDown is not null || canvas.OnPointerMove is not null
            || canvas.OnPointerUp is not null || canvas.OnPointerLeave is not null)
        {
            s.Input.Add(new CanvasRegion(s.Node.Bounds, canvas, s.Node.Path ?? ""));
        }
    }

    private void EmitSpinnerNode(Spinner spinner, EmitState s)
    {
        EmitSpinner(s.Node, spinner, s.Theme, s.Mode, s.Builder, s.Motion);
    }

    /// <summary>
    /// The live camera surface. The session mutates ONE byte array and bumps a version; this keeps
    /// ONE TextureData wrapping that array and mirrors the version, so the renderer's identity
    /// cache re-uploads the same GPU slot instead of minting a leaked texture per frame. While a
    /// session is on screen the frame clock keeps turning — video IS motion.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ICameraSession, TextureData>
        LiveFrames = new();

    /// <summary>
    /// W4 images: decode through the platform loader (cached per source), draw as an Rgba8 Texture
    /// command with the FIT math — Stretch fills, Contain letter-boxes centered, Cover fills and
    /// CLIPS to the node's rrect. No loader / failed decode → the SurfaceSubtle placeholder box.
    /// Nearest sampling v1 (bilinear is the scaling-quality fence).
    /// </summary>
    private static void EmitImage(LayoutNode node, Image image, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, MotionScope motion)
    {
        TextureData? data = null;
        if (motion.ImageLoader is { } loader)
        {
            var cache = motion.ImageCache;
            if (cache is null || !cache.TryGetValue(image.Source, out data))
            {
                var decoded = loader.Load(image.Source);
                data = decoded is null ? null : TextureData.Rgba(decoded.Width, decoded.Height, decoded.Rgba);
                cache?[image.Source] = data;
            }
        }
        if (data is null)
        {
            builder.FillRRect(new RRect(node.Bounds, image.CornerRadius),
                Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
            return;
        }

        var b = node.Bounds;
        Rect dest;
        var clip = !image.CornerRadius.IsZero;
        switch (image.Fit)
        {
            case ImageFit.Stretch:
                dest = b;
                break;
            case ImageFit.Contain:
            {
                var scale = MathF.Min(b.Width / data.Width, b.Height / data.Height);
                var w = data.Width * scale;
                var h = data.Height * scale;
                dest = new Rect(b.X + (b.Width - w) / 2, b.Y + (b.Height - h) / 2, w, h);
                break;
            }
            default: // Cover — fills and overflows; always clipped to the bounds.
            {
                var scale = MathF.Max(b.Width / data.Width, b.Height / data.Height);
                var w = data.Width * scale;
                var h = data.Height * scale;
                dest = new Rect(b.X + (b.Width - w) / 2, b.Y + (b.Height - h) / 2, w, h);
                clip = true;
                break;
            }
        }

        if (clip) builder.PushClip(new RRect(b, image.CornerRadius));
        builder.Texture(dest, new Color(255, 255, 255, 255), data);
        if (clip) builder.PopClip();
    }

    private static void EmitCameraPreview(LayoutNode node, CameraPreview camera, IAppTheme theme,
        ThemeMode mode, DisplayListBuilder builder, MotionScope motion)
    {
        var rrect = new RRect(node.Bounds, camera.CornerRadius);
        if (camera.Session is not { FrameBytes: { } bytes, FrameWidth: > 0 } session)
        {
            // Not started (or no pixels yet): the same SurfaceSubtle placeholder Image degrades to.
            builder.FillRRect(rrect, Paint.Solid(theme.SurfaceSubtle.Resolve(mode)));
            return;
        }

        motion.Active = true;   // frames keep arriving; the host must keep asking for them

        if (!LiveFrames.TryGetValue(session, out var texture)
            || texture.Width != session.FrameWidth || texture.Height != session.FrameHeight
            || !ReferenceEquals(texture.Alpha, bytes))
        {
            texture = TextureData.Rgba(session.FrameWidth, session.FrameHeight, bytes);
            LiveFrames.Remove(session);
            LiveFrames.Add(session, texture);
        }
        texture.Version = session.FrameVersion;

        builder.PushClip(rrect);
        builder.Texture(CoverRect(node.Bounds, session.FrameWidth, session.FrameHeight),
            Color.White, texture);
        builder.PopClip();
    }

    /// <summary>Center-crop: the rect that fills the slot at the source's aspect (ImageFit.Cover).</summary>
    private static Rect CoverRect(in Rect slot, float sourceW, float sourceH)
    {
        var scale = MathF.Max(slot.Width / sourceW, slot.Height / sourceH);
        var w = sourceW * scale;
        var h = sourceH * scale;
        return new Rect(slot.X + (slot.Width - w) / 2, slot.Y + (slot.Height - h) / 2, w, h);
    }

    /// <summary>
    /// Spec B15, drawn INSIDE the fence: 8 rrect bars (2×5 in the 16dp em-box, scaled), rotated
    /// i·45° about the center, opacity phase-staggered on the 800ms/rev linear clock — a pure
    /// function of the frame time (golden-testable at fixed t). Reduce Motion keeps the fade but
    /// drops the rotation phase: every bar pulses IN PLACE with the same alpha (spec B15) — the
    /// spinner therefore always reports active motion (it is a functional indicator, not
    /// decoration).
    /// </summary>
    private static void EmitSpinner(LayoutNode node, Spinner spinner, IAppTheme theme, ThemeMode mode,
        DisplayListBuilder builder, MotionScope motion)
    {
        motion.Active = true;
        var phase = motion.TimeMs % Spinner.RevolutionMs / Spinner.RevolutionMs;
        var tint = (spinner.Color ?? theme.TextPrimary).Resolve(mode);

        var scale = node.Bounds.Width / 16f;
        var centerX = node.Bounds.X + node.Bounds.Width / 2;
        var centerY = node.Bounds.Y + node.Bounds.Height / 2;
        var bar = new RRect(
            new Rect(centerX - scale, node.Bounds.Y, 2 * scale, 5 * scale),
            new CornerRadii(scale));

        for (var i = 0; i < 8; i++)
        {
            // Web parity: bar i runs the same 1→0.3 sawtooth with a -i·(rev/8) delay; Reduce
            // Motion zeroes the stagger (all bars share the pulse).
            var k = motion.Reduced ? phase : (((i - phase * 8) % 8) + 8) % 8 / 8;
            var alpha = 1f - 0.7f * k;

            builder.PushTransform(
                Matrix2D.Translation(-centerX, -centerY)
                * Matrix2D.Rotation(i * 45 * MathF.PI / 180)
                * Matrix2D.Translation(centerX, centerY));
            builder.FillRRect(bar, Paint.Solid(tint.WithOpacity(alpha)));
            builder.Pop();
        }
    }
}
