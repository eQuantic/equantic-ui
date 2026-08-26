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

    /// <summary>W4: fill <see cref="DrawCommand.Shape"/>'s rect sampling the A8 texture
    /// (<see cref="DrawCommand.TextureId"/>) as coverage, tinted by the paint color — text
    /// blocks and (later) images. Nearest sampling: rasters are generated at device scale.</summary>
    Texture = 6,

    /// <summary>
    /// Frosted glass (W3): everything drawn SO FAR, blurred by the dual-Kawase pyramid at
    /// <see cref="DrawCommand.StrokeWidth"/> (device px), composites back INSIDE
    /// <see cref="DrawCommand.Clip"/> — the region rrect, baked to device space by the builder
    /// exactly like a clip. Draw it BEFORE the translucent fill that sits on the glass. FENCE: only
    /// honored at the top level — inside a group-opacity layer the command is skipped (the layer is
    /// isolated from the backdrop; blurring through it needs the layer's own backdrop chain).
    /// </summary>
    BackdropBlur = 7,

    /// <summary>
    /// W1 (docs/DESKTOP-PLAN.md): fill an ANNULAR SECTOR — a ring slice. One more shape in the SDF
    /// family, NOT a path engine. Slot reuse, the flat-command idiom StrokeWidth already set:
    /// <see cref="DrawCommand.Shape"/>.Rect is the bounding square (center + outer radius), and
    /// Radii carries [inner radius, start angle, end angle, corner rounding] — angles in radians,
    /// 0 along +X, increasing toward +Y (y grows down: clockwise on screen).
    /// </summary>
    FillAnnularSector = 8,
}

/// <summary>
/// One flat, heap-free draw command. <see cref="Shape"/> is in LOCAL space; <see cref="Transform"/>
/// maps local → device (baked by the builder from its transform stack, so rasterizers never track
/// state). Radii are pre-normalized by the builder.
/// </summary>
/// <summary>
/// An A8 coverage bitmap a draw command samples (W4 text: one raster per Text block, tinted by the
/// command's paint). Referenced by index into the display list's texture table; equality is
/// IDENTITY (the caches reuse instances), never a byte compare.
/// </summary>
public sealed class TextureData
{
    public TextureData(int width, int height, byte[] alpha)
    {
        Width = width;
        Height = height;
        Alpha = alpha;
        Format = TextureFormat.A8;
    }

    private TextureData(int width, int height, byte[] pixels, TextureFormat format)
    {
        Width = width;
        Height = height;
        Alpha = pixels;
        Format = format;
    }

    /// <summary>W4 images: straight sRGB RGBA bytes (4 per pixel) — rasterizers premultiply.</summary>
    public static TextureData Rgba(int width, int height, byte[] rgba) =>
        new(width, height, rgba, TextureFormat.Rgba8);

    public int Width { get; }
    public int Height { get; }

    /// <summary>A8: one coverage byte per pixel. Rgba8: 4 straight-sRGB bytes per pixel.</summary>
    public byte[] Alpha { get; }

    public TextureFormat Format { get; }

    /// <summary>
    /// Bumped after mutating <see cref="Alpha"/> IN PLACE — a live camera writes each frame into
    /// the same instance and increments this, and the renderer re-uploads when the number moved.
    /// Static rasters never touch it, and their cache behaviour is exactly what it was. This is
    /// what keeps a 30 fps feed from minting a new instance per frame into an identity-keyed cache
    /// that never evicts — which would be a GPU leak with a frame rate.
    /// </summary>
    public int Version { get; set; }
}

/// <summary>How a <see cref="TextureData"/>'s bytes are interpreted.</summary>
public enum TextureFormat : byte
{
    /// <summary>Coverage-only; the draw command's paint is the color (text, icons).</summary>
    A8 = 0,

    /// <summary>Straight sRGB color with alpha (images) — the paint tint is ignored.</summary>
    Rgba8 = 1,
}

public readonly record struct DrawCommand
{
    public DrawCommandKind Kind { get; init; }
    public RRect Shape { get; init; }
    public Paint Paint { get; init; }
    public float StrokeWidth { get; init; }
    public Matrix2D Transform { get; init; }

    /// <summary>Index into <see cref="DisplayList.Textures"/> for <see cref="DrawCommandKind.Texture"/>;
    /// -1 otherwise.</summary>
    public int TextureId { get; init; }

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
    private static readonly TextureData[] NoTextures = Array.Empty<TextureData>();
    private readonly DrawCommand[] _commands;
    private readonly TextureData[] _textures;

    internal DisplayList(DrawCommand[] commands, TextureData[]? textures = null)
    {
        _commands = commands;
        _textures = textures ?? NoTextures;
    }

    public ReadOnlySpan<DrawCommand> Commands => _commands;
    public int Count => _commands.Length;

    /// <summary>The frame's texture table (<see cref="DrawCommand.TextureId"/> indexes here).</summary>
    public IReadOnlyList<TextureData> Textures => _textures;
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

    private readonly List<TextureData> _textures = new();

    /// <summary>Registers a texture for this frame's list and returns its id (identity dedupe —
    /// the text cache hands the SAME instance every frame, so each raster uploads/registers once).</summary>
    public int RegisterTexture(TextureData texture)
    {
        for (var i = 0; i < _textures.Count; i++)
            if (ReferenceEquals(_textures[i], texture)) return i;
        _textures.Add(texture);
        return _textures.Count - 1;
    }

    /// <summary>W4: draw <paramref name="texture"/> into <paramref name="rect"/> tinted by
    /// <paramref name="tint"/> (the A8 raster is pure coverage; color lives here).</summary>
    public void Texture(in Rect rect, Color tint, TextureData texture) =>
        Texture(rect, Paint.Solid(tint), texture);

    /// <summary>
    /// W4 + gradient text: the tint may be a PAINT, so glyph coverage can be filled with a
    /// gradient instead of a flat color. The gradient's axis is in LOCAL space (the same space the
    /// SDF path uses), and both rasterizers evaluate it through <see cref="Paint.ColorAt"/>.
    /// </summary>
    public void Texture(in Rect rect, in Paint tint, TextureData texture) => _commands.Add(new DrawCommand
    {
        Kind = DrawCommandKind.Texture,
        Shape = new RRect(rect, default),
        Paint = tint,
        TextureId = RegisterTexture(texture),
        Transform = _current,
        Clip = _clip,
    });

    public void FillRRect(in RRect shape, in Paint paint) => _commands.Add(new DrawCommand
    {
        Kind = DrawCommandKind.FillRRect,
        Shape = shape.Normalized(),
        Paint = paint,
        Transform = _current,
        Clip = _clip,
    });

    /// <summary>A filled circle — sugar over <see cref="FillRRect"/> at full radius, here because
    /// the F0 consumer's hub reached for an annular sector with rIn 0 and sweep 2π to draw one.</summary>
    public void FillCircle(Point center, float radius, in Paint paint)
    {
        if (radius <= 0) return;
        var rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        FillRRect(new RRect(rect, new CornerRadii(radius)), paint);
    }

    /// <summary>
    /// W1: an annular sector (ring slice) around <paramref name="center"/>, from
    /// <paramref name="startAngle"/> to <paramref name="endAngle"/> (radians, 0 = +X, increasing
    /// toward +Y — clockwise on screen, y grows down). <paramref name="rounding"/> rounds all four
    /// corners. Sweep is capped at 2π; a full ring is sweep 2π with rounding 0.
    /// </summary>
    public void FillAnnularSector(Point center, float innerRadius, float outerRadius,
        float startAngle, float endAngle, in Paint paint, float rounding = 0)
    {
        if (outerRadius <= 0 || endAngle <= startAngle) return;
        innerRadius = Math.Clamp(innerRadius, 0, outerRadius);
        endAngle = MathF.Min(endAngle, startAngle + 2 * MathF.PI);
        // Rounding beyond half the band inverts the inset shape; clamp is the honest degenerate.
        rounding = Math.Clamp(rounding, 0, (outerRadius - innerRadius) / 2);
        var rect = new Rect(center.X - outerRadius, center.Y - outerRadius, outerRadius * 2, outerRadius * 2);
        _commands.Add(new DrawCommand
        {
            Kind = DrawCommandKind.FillAnnularSector,
            // NOT Normalized(): these slots are not corner radii (see the kind's doc), and the
            // normalizer would clamp the angles against the rect.
            Shape = new RRect(rect, new CornerRadii(innerRadius, startAngle, endAngle, rounding)),
            Paint = paint,
            Transform = _current,
            Clip = _clip,
        });
    }

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

    /// <summary>
    /// Records a frosted-glass region: everything drawn so far blurs by <paramref name="radius"/>
    /// (local px — scaled to device here) inside <paramref name="shape"/>, whose rrect is baked to
    /// device space with the same math as <see cref="PushClip"/> and intersected with the ambient
    /// clip. Record it BEFORE the glass surface's own translucent fill.
    /// </summary>
    public void BackdropBlur(in RRect shape, float radius)
    {
        var scale = _current.AverageScale();
        if (radius * scale <= 0) return;
        var normalized = shape.Normalized();
        var deviceRect = _current.TransformBounds(normalized.Rect);
        var radii = new CornerRadii(
            normalized.Radii.TopLeft * scale, normalized.Radii.TopRight * scale,
            normalized.Radii.BottomRight * scale, normalized.Radii.BottomLeft * scale);
        var device = new RRect(deviceRect, radii).Normalized();
        var region = _clip is { } outer
            ? new RRect(outer.Rect.Intersect(device.Rect), device.Radii).Normalized()
            : device;
        if (region.Rect.IsEmpty) return;
        _commands.Add(new DrawCommand
        {
            Kind = DrawCommandKind.BackdropBlur,
            Shape = normalized,
            StrokeWidth = radius * scale,
            Transform = _current,
            Clip = region,
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
    /// <summary>Commands recorded so far — marks a subtree's start for presence-exit snapshots.</summary>
    public int CommandCount => _commands.Count;

    /// <summary>Copies the commands recorded since <paramref name="start"/> — the presence-exit
    /// snapshot (a departed subtree replays these while its exit motion runs).</summary>
    public DrawCommand[] CommandsFrom(int start)
    {
        var slice = new DrawCommand[_commands.Count - start];
        _commands.CopyTo(start, slice, 0, slice.Length);
        return slice;
    }

    /// <summary>
    /// Re-records an ALREADY-BAKED command (a presence-exit replay), shifted by
    /// <paramref name="offset"/> — a pure translation in DEVICE space, which also moves the baked
    /// clip so clipped content travels with its own pixels.
    /// <para>
    /// The offset is passed rather than taken from the current transform, and that distinction is
    /// the whole bug this signature exists to prevent. A baked command already carries every
    /// transform that was in effect when it was recorded — including the root scale on a retina
    /// display — so composing with the current transform applied that scale a SECOND time and threw
    /// the departing layer down and to the right for the one frame before it faded. Invisible at
    /// scale 1, which is every headless test.
    /// </para>
    /// </summary>
    public void Replay(in DrawCommand command, in Matrix2D offset)
    {
        if (offset.IsIdentity)
        {
            _commands.Add(command);
            return;
        }

        var clip = command.Clip;
        if (clip is { } baked)
        {
            var r = baked.Rect;
            clip = baked with { Rect = new Rect(r.X + offset.M31, r.Y + offset.M32, r.Width, r.Height) };
        }
        _commands.Add(command with { Transform = command.Transform * offset, Clip = clip });
    }

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

    /// <summary>
    /// Ready for the next frame, keeping every internal buffer's CAPACITY — the render loop hands
    /// the same builder to every frame instead of growing fresh lists 120 times a second.
    /// </summary>
    public void Reset()
    {
        _commands.Clear();
        _textures.Clear();
        _stack.Clear();
        _clipStack.Clear();
        _current = Matrix2D.Identity;
        _clip = null;
        _openLayers = 0;
    }

    public DisplayList Build()
    {
        if (_stack.Count != 0)
            throw new InvalidOperationException($"Unbalanced transform stack: {_stack.Count} unpopped PushTransform call(s).");
        if (_clipStack.Count != 0)
            throw new InvalidOperationException($"Unbalanced clip stack: {_clipStack.Count} unpopped PushClip call(s).");
        if (_openLayers != 0)
            throw new InvalidOperationException($"Unbalanced layer stack: {_openLayers} unpopped PushLayer call(s).");
        return new DisplayList(_commands.ToArray(), _textures.Count > 0 ? _textures.ToArray() : null);
    }
}
