using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine;

// The fine-grained RHI (plan W1), EXTRACTED from the Metal spike's proven shape rather than
// designed ahead of it (the deliberate deferral noted on IRenderBackend). The surface is exactly
// what the spike's RenderCore needed and nothing more: create render targets and sampled textures,
// begin a pass that clears, then per draw — select one of the FIXED pipelines (D5), bind a texture,
// push 160 bytes of uniforms, draw the fullscreen triangle. GPU backends (Metal, Vulkan) implement
// these interfaces; the display-list encoding loop lives ONCE in <see cref="RhiRenderer"/> so the
// backends can only differ in API calls, never in frame semantics. The Reference backend stays a
// direct IRenderBackend — it rasterizes commands on the CPU and has no device to abstract (D7 is
// about sitting behind the coarse seam, which it does).

/// <summary>
/// The fixed, enumerable pipeline registry (plan D5): every draw the engine encodes selects one of
/// these precompiled pipelines. Backends create them per target format at init (from the offline
/// Slang outputs, plan D3) and only SELECT here — creating a pipeline at draw time is a bug by
/// definition.
/// </summary>
public enum RhiPipelineKind : byte
{
    /// <summary>Analytic SDF fill/stroke/shadow with optional gradient — <c>sdf_fragment</c>.</summary>
    Sdf = 0,

    /// <summary>A8 coverage raster × paint tint, nearest texel Load (text, icons) — <c>textured_fragment</c>.</summary>
    TexturedA8 = 1,

    /// <summary>Straight-sRGB RGBA raster, texel color wins over tint (images) — <c>textured_rgba_fragment</c>.</summary>
    TexturedRgba = 2,

    /// <summary>
    /// Composites an offscreen GROUP-OPACITY layer at its alpha — <c>layer_composite</c>. Its
    /// source is a render target, whose texels are already PREMULTIPLIED, so the math is a scale
    /// rather than the straight-alpha premultiply <see cref="TexturedRgba"/> performs. Sharing the
    /// image pipeline here would double-apply alpha.
    /// </summary>
    LayerComposite = 3,

    /// <summary>Dual-Kawase pyramid, downsampling half — <c>blur_down</c> (plan W3). Blending is
    /// OFF: each level REPLACES its target. Requires the LINEAR filter (D6b).</summary>
    BlurDown = 4,

    /// <summary>Dual-Kawase pyramid, upsampling half — <c>blur_up</c>. Blending OFF; LINEAR.</summary>
    BlurUp = 5,

    /// <summary>
    /// A tile layer (#1937): one Rgba8 tile repeated over the command's area — <c>tiled_rgba_fragment</c>.
    /// It filters linearly between four texel LOADS in premultiplied light, wrapping where tiles touch,
    /// which is <see cref="Tiling.Sample"/> on the GPU: no sampler, so a GPU's own filter weights cannot
    /// set it apart from the Reference backend's.
    /// </summary>
    TiledRgba = 6,
}

/// <summary>Texture filtering for a binding (D6b). Nearest is the DEFAULT and stays correct for
/// device-scale rasters (glyphs, icons — texels map 1:1); Linear serves the paths that genuinely
/// filter: the blur pyramid and, later, scaled images.</summary>
public enum RhiFilter : byte
{
    Nearest = 0,
    Linear = 1,
}

[StructLayout(LayoutKind.Sequential)]
public struct Float4
{
    public float X, Y, Z, W;

    public Float4(float x, float y, float z, float w)
    {
        X = x; Y = y; Z = z; W = w;
    }
}

/// <summary>
/// The per-draw uniform block — matches the normative Slang source's <c>DrawUniforms</c> layout
/// (ten float4s, 160 bytes) on every backend: Metal receives it via <c>setFragmentBytes</c>,
/// Vulkan via a dynamic-offset uniform ring (160 bytes exceeds the 128-byte push-constant floor
/// the spec guarantees, so push constants can't carry it across the Android driver zoo).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DrawUniforms
{
    public Float4 Inv0;      // inverse transform: m11, m21, m31, deviceScale
    public Float4 Inv1;      // inverse transform: m12, m22, m32, strokeWidth
    public Float4 Rect;      // center.x, center.y, halfSize.w, halfSize.h
    public Float4 Radii;     // tl, tr, br, bl (normalized)
    public Float4 ColorA;    // sRGB 0..1 straight alpha (solid / gradient start)
    public Float4 ColorB;    // sRGB 0..1 straight alpha (gradient end)
    public Float4 Gradient;  // start.x, start.y, end.x, end.y (LOCAL space) | texture w/h for textured draws
    public Float4 Flags;     // x: 0 fill | 1 stroke | 2 shadow · y: 1 = linear gradient · z: 1 = clipped
    public Float4 ClipRect;  // DEVICE-space clip: center.x, center.y, halfSize.w, halfSize.h
    public Float4 ClipRadii; // tl, tr, br, bl

    /// <summary>
    /// Builds the block for one SDF-family command (the shared encode math the Metal spike proved
    /// at ±1 LSB). False when the command's transform is non-invertible or scale-degenerate —
    /// nothing to draw.
    /// </summary>
    public static bool TryBuild(in DrawCommand command, out DrawUniforms uniforms)
    {
        uniforms = default;
        var inverse = command.Transform.Invert();
        if (inverse is null) return false;
        var inv = inverse.Value;
        var scale = command.Transform.AverageScale();
        if (scale <= 0) return false;

        var shape = command.Shape;
        var paint = command.Paint;

        uniforms.Inv0 = new Float4(inv.M11, inv.M21, inv.M31, scale);
        uniforms.Inv1 = new Float4(inv.M12, inv.M22, inv.M32, command.StrokeWidth);
        uniforms.Rect = new Float4(shape.Rect.Center.X, shape.Rect.Center.Y, shape.Rect.Width / 2, shape.Rect.Height / 2);
        uniforms.Radii = new Float4(shape.Radii.TopLeft, shape.Radii.TopRight, shape.Radii.BottomRight, shape.Radii.BottomLeft);
        uniforms.ColorA = ToFloat4(paint.Color);
        uniforms.ColorB = ToFloat4(paint.EndColor);
        uniforms.Gradient = new Float4(paint.GradientStart.X, paint.GradientStart.Y, paint.GradientEnd.X, paint.GradientEnd.Y);
        uniforms.Flags = new Float4(
            command.Kind switch
            {
                DrawCommandKind.StrokeRRect => 1,
                DrawCommandKind.ShadowRRect => 2,
                DrawCommandKind.FillAnnularSector => 3,
                _ => 0,
            },
            // 0 solid · 1 linear · 2 radial — the paint kinds share the `Gradient` slot, so a radial
            // needed no new uniform: center rides .xy and the radii .zw.
            paint.Kind switch { PaintKind.LinearGradient => 1, PaintKind.RadialGradient => 2, _ => 0 },
            command.Clip is null ? 0 : 1, 0);
        if (command.Clip is { } clip)
        {
            uniforms.ClipRect = new Float4(clip.Rect.Center.X, clip.Rect.Center.Y, clip.Rect.Width / 2, clip.Rect.Height / 2);
            uniforms.ClipRadii = new Float4(clip.Radii.TopLeft, clip.Radii.TopRight, clip.Radii.BottomRight, clip.Radii.BottomLeft);
        }
        return true;
    }

    /// <summary>
    /// Builds the block for a <see cref="DrawCommandKind.TiledTexture"/> command, its tiling in the slots
    /// <c>textured_rgba_fragment</c> leaves unused, so the block keeps its layout: <c>Radii</c> holds the
    /// origin and the period, <c>ColorA</c> the extent and the opacity, <c>ColorB</c> whether each axis
    /// repeats and whether it wraps, and <c>Gradient</c> the texture's size. A group's opacity is not
    /// here: the group draws into a layer of its own and composites once (#1952). False when the
    /// command's transform cannot be inverted.
    /// </summary>
    public static bool TryBuildTiled(in DrawCommand command, TextureData texture, out DrawUniforms uniforms)
    {
        if (!TryBuild(in command, out uniforms)) return false;
        var tiling = command.Tiling;
        uniforms.Radii = new Float4(tiling.Origin.X, tiling.Origin.Y, tiling.Period.Width, tiling.Period.Height);
        uniforms.ColorA = new Float4(tiling.Extent.Width, tiling.Extent.Height, 0, command.StrokeWidth);
        uniforms.ColorB = new Float4(tiling.RepeatX ? 1 : 0, tiling.RepeatY ? 1 : 0, tiling.WrapsX ? 1 : 0, tiling.WrapsY ? 1 : 0);
        uniforms.Gradient = new Float4(texture.Width, texture.Height, 0, 0);
        uniforms.Flags = new Float4(0, 0, command.Clip is null ? 0 : 1, 0);
        return true;
    }

    private static Float4 ToFloat4(Color color) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
}

/// <summary>
/// A GPU texture the encoder can bind for sampling (A8 coverage or straight-sRGB RGBA).
/// <para>
/// DISPOSED WHILE WORK MAY STILL READ IT. The renderer lets a texture go by what frames draw, and
/// knows nothing of what the GPU has finished, so the lifetime is the backend's to keep: a texture
/// stays alive until the work already submitted that reads it is done. Metal's command buffers
/// retain what they bind; the Vulkan backend waits for each frame before the next begins. A backend
/// that stops waiting defers the destroy itself.
/// </para>
/// </summary>
public interface IRhiTexture : IDisposable
{
    int Width { get; }
    int Height { get; }
}

/// <summary>An offscreen render target (<c>RGBA8Unorm_sRGB</c>) the harness can read back.</summary>
public interface IRhiRenderTarget : IRhiTexture
{
    /// <summary>
    /// Reads back as tightly-packed straight-alpha sRGB RGBA scanlines (the golden interchange
    /// format) — a test/tooling path, never a frame-loop path. Backends store premultiplied
    /// pixels, so implementations funnel through <see cref="RhiReadback.UnpremultiplySrgb"/>.
    /// </summary>
    void ReadPixelsSrgb(Span<byte> destinationRgba);
}

/// <summary>
/// One recorded pass over one render target: pipeline selection, texture binds and uniform+draw
/// pairs, submitted once. Mirrors the engine's D4 shape — one main pass per frame, one fullscreen
/// triangle per draw; there is deliberately no way to express anything else through this interface.
/// </summary>
public interface IRhiCommandList
{
    /// <summary>Selects one of the fixed pipelines (D5), resolved against the pass target's format.</summary>
    void SetPipeline(RhiPipelineKind kind);

    /// <summary>
    /// Binds <paramref name="texture"/> to <paramref name="slot"/> —
    /// <see cref="RhiRenderer.CoverageSlot"/> (A8) or <see cref="RhiRenderer.ColorSlot"/> (RGBA),
    /// the normative shader's binding order after the uniform block. <paramref name="filter"/>
    /// selects the sampler the FILTERED paths read through (binding 3); Load-based paths ignore it.
    /// </summary>
    void BindTexture(int slot, IRhiTexture texture, RhiFilter filter = RhiFilter.Nearest);

    /// <summary>Pushes the 160-byte uniform block and draws the fullscreen triangle (3 vertices).</summary>
    void Draw(in DrawUniforms uniforms);

    /// <summary>
    /// Ends the pass and submits. Offscreen callers wait (they read pixels right after); window
    /// paths present asynchronously and let the swapchain's backpressure pace the CPU.
    /// </summary>
    void Submit(bool waitUntilCompleted);
}

/// <summary>
/// A GPU device behind the RHI: creates the fixed pipeline set at init (D5), creates targets and
/// textures, begins passes. One per process/backend; owned by the <see cref="IRenderBackend"/>
/// adapter that fronts it.
/// </summary>
public interface IRhiDevice : IDisposable
{
    IRhiRenderTarget CreateRenderTarget(int width, int height);

    /// <summary>Creates and uploads an immutable sampled texture (A8 → R8, Rgba8 → RGBA8_sRGB).</summary>
    IRhiTexture CreateTexture(TextureData data);

    /// <summary>Begins a pass that clears <paramref name="target"/> to <paramref name="clearColor"/>
    /// (converted to premultiplied linear by the backend — the pass clear happens pre-encoding).</summary>
    IRhiCommandList Begin(IRhiRenderTarget target, Color clearColor);
}

/// <summary>
/// The SHARED display-list encoder — the Metal spike's RenderCore loop, hoisted above the RHI so
/// every GPU backend runs the identical frame semantics: leading Clear becomes the pass clear, each
/// group-opacity layer draws into a target of its own and composites once, each backdrop splits the
/// frame, Texture commands switch to the textured pipelines, and each command becomes one uniform
/// block + one fullscreen-triangle draw. Owns the per-device sampled-texture cache (keyed by
/// <see cref="TextureData"/> IDENTITY — the raster caches reuse instances) and the pool of targets a
/// frame's own passes draw into.
/// <para>
/// ONE way in, <see cref="Render(DisplayList, IRhiFrameTarget)"/>, for a window and an offscreen
/// target alike (#1952). The windows used to open their own pass and encode into it, skipping the
/// phase that draws the layers, so a group's opacity blended each of its draws on its own and a
/// backdrop drew no blur on screen, while the goldens, which render offscreen, saw neither.
/// </para>
/// <para>
/// What it holds follows what is DRAWN: a raster no display list has drawn for
/// <see cref="RetainFrames"/> frames gives its GPU texture back (#858). Kept for the renderer's
/// life instead, every text that ever changed stayed uploaded, and on Vulkan each one held
/// descriptor sets out of a pool of a fixed size: an app that drew a thousand different texts ended
/// there.
/// </para>
/// </summary>
public sealed class RhiRenderer : IDisposable
{
    /// <summary>Texture slot for A8 coverage rasters (<c>coverageTexture</c>, binding 1).</summary>
    public const int CoverageSlot = 0;

    /// <summary>Texture slot for straight-sRGB RGBA images (<c>colorTexture</c>, binding 2).</summary>
    public const int ColorSlot = 1;

    /// <summary>
    /// How many frames a raster may go undrawn and stay uploaded. Long enough that what leaves the
    /// screen for a moment (a row scrolled past and back, a state that flickers) is not uploaded
    /// again; short enough that a text changing every frame holds a handful of textures rather
    /// than all of them.
    /// <para>
    /// It is a number about uploads, NOT a fence against the GPU: a window may have more frames in
    /// flight than this, and what keeps a released texture alive for them is the backend (see
    /// <see cref="IRhiTexture"/>).
    /// </para>
    /// <para>
    /// FRAMES, and the shells render on demand: a screen that goes still stops the count, and what
    /// its last frames stopped drawing waits for the next ones. Nothing is uploaded while nothing
    /// is drawn, so the wait holds what was there and adds nothing to it.
    /// </para>
    /// </summary>
    internal const int RetainFrames = 30;

    /// <summary>
    /// How many rasters a frame releases when no more than that were uploaded by the frame before.
    /// Leaving a screen retires all of its rasters in the same frame, and each release costs the
    /// backend a destroy; spread over a few frames it is not a hitch in the transition that follows.
    /// <para>
    /// A SHARE, not a ceiling. A frame releases at least as many rasters as the one before it
    /// uploaded, so what the renderer gives back keeps pace with what it takes: a screen that draws
    /// a hundred new rasters every frame releases a hundred a frame, where a fixed share would let
    /// the difference accumulate for as long as the screen ran.
    /// </para>
    /// </summary>
    internal const int ReleasesPerFrame = 64;

    private readonly IRhiDevice _device;
    private readonly Dictionary<TextureData, Uploaded> _textures = new();
    private readonly List<TextureData> _retired = new();
    private readonly RhiTargetPool _pool;
    private readonly LayerTargets _layers = new();
    private long _frame;
    private int _uploads;   // new rasters uploaded by the frame being encoded
    private IRhiTexture? _dummy;

    /// <summary>A raster on the GPU: the texture, the version of the bytes it holds, and the last
    /// frame that drew it.</summary>
    private sealed class Uploaded(IRhiTexture texture, int version, long frame)
    {
        public IRhiTexture Texture = texture;
        public int Version = version;
        public long LastFrame = frame;
    }

    public RhiRenderer(IRhiDevice device)
    {
        _device = device;
        _pool = new RhiTargetPool(device);
    }

    /// <summary>
    /// The pass clear for <paramref name="displayList"/>: its leading Clear command's color
    /// (engine lists always start with one), or transparent. The frame's first pass clears to it, and
    /// the encoder then skips Clear commands.
    /// </summary>
    public static Color ClearColorOf(DisplayList displayList)
    {
        var commands = displayList.Commands;
        return commands.Length > 0 && commands[0].Kind == DrawCommandKind.Clear
            ? commands[0].Paint.Color
            : Color.Transparent;
    }

    /// <summary>
    /// Offscreen: the frame's passes into <paramref name="target"/>, the last one waited for, since the
    /// caller reads its pixels right after. The same path a window takes, only the last pass differs.
    /// </summary>
    public void Render(DisplayList displayList, IRhiRenderTarget target) =>
        Render(displayList, new OffscreenFrame(_device, target));

    /// <summary>
    /// Draws one frame into <paramref name="frame"/>, through every phase, the one entry a window and
    /// an offscreen target both take (#1952):
    /// <list type="number">
    /// <item>each group-opacity layer into a target of its own, innermost first;</item>
    /// <item>each backdrop outside a layer splits the frame: what is drawn so far blurs, and the next
    /// pass starts from it;</item>
    /// <item>the last pass, into <paramref name="frame"/>, compositing each layer once.</item>
    /// </list>
    /// Every pass before the last draws into a target from the pool and is submitted and waited for
    /// before the next begins, so the last pass reads them finished on every backend, a queue that
    /// runs its submissions apart included.
    /// </summary>
    public void Render(DisplayList displayList, IRhiFrameTarget frame)
    {
        BeginFrame(frame.Width, frame.Height);
        try
        {
            var layers = RenderLayers(displayList, frame.Width, frame.Height);
            RenderPasses(displayList, frame, layers, FindTopLevelBackdrops(displayList.Commands));
        }
        finally
        {
            _layers.Return(_pool);
        }
    }

    /// <summary>The last pass offscreen: a render target the device begins, waited for when it submits.</summary>
    private sealed class OffscreenFrame(IRhiDevice device, IRhiRenderTarget target) : IRhiFrameTarget
    {
        public int Width => target.Width;
        public int Height => target.Height;
        public IRhiCommandList Begin(Color clearColor) => device.Begin(target, clearColor);
        public void Submit(IRhiCommandList commands) => commands.Submit(waitUntilCompleted: true);
    }

    /// <summary>Indices of BackdropBlur commands OUTSIDE layer scopes (the fence: inside a
    /// group-opacity layer the backdrop is isolated and the command is skipped). Null when none —
    /// the common frame is one pass and allocates nothing for the splits.</summary>
    private static List<int>? FindTopLevelBackdrops(ReadOnlySpan<DrawCommand> cmds)
    {
        List<int>? splits = null;
        var depth = 0;
        for (var i = 0; i < cmds.Length; i++)
        {
            switch (cmds[i].Kind)
            {
                case DrawCommandKind.BeginLayer: depth++; break;
                case DrawCommandKind.EndLayer: depth--; break;
                case DrawCommandKind.BackdropBlur when depth == 0 && cmds[i].Clip is not null:
                    (splits ??= new()).Add(i);
                    break;
            }
        }
        return splits;
    }

    /// <summary>
    /// The frame's passes after its layers. With no backdrop that is one pass, into
    /// <paramref name="frame"/>. Each BackdropBlur (W3) SPLITS the frame: the content so far
    /// accumulates in a pooled target, runs the pyramid, and the next pass seeds itself by compositing
    /// that content plus the blurred copy clipped to the region rrect, then keeps drawing. The LAST
    /// segment draws into <paramref name="frame"/>. Metal and Vulkan allow one open pass per encoder,
    /// so "read what you just drew" is necessarily a pass boundary.
    /// </summary>
    private void RenderPasses(DisplayList displayList, IRhiFrameTarget frame, LayerTargets? layers, List<int>? splits)
    {
        var cmds = displayList.Commands;
        var splitCount = splits?.Count ?? 0;

        IRhiRenderTarget? content = null;  // the accumulated frame BELOW the current split
        IRhiRenderTarget? blurred = null;  // its pyramid output
        var start = 0;
        for (var s = 0; s <= splitCount; s++)
        {
            var isLast = s == splitCount;
            var end = isLast ? cmds.Length : splits![s];
            var clear = s == 0 ? ClearColorOf(displayList) : Color.Transparent;
            var segment = isLast ? null : _pool.Rent(frame.Width, frame.Height);
            var commands = segment is null ? frame.Begin(clear) : _device.Begin(segment, clear);
            if (content is not null && blurred is not null)
            {
                // Seed: full content below, then the blurred backdrop inside the glass rrect.
                commands.SetPipeline(RhiPipelineKind.Sdf);
                commands.BindTexture(CoverageSlot, Dummy());
                commands.BindTexture(ColorSlot, Dummy());
                var active = RhiPipelineKind.Sdf;
                CompositeLayer(commands, content, 1f, ref active);
                CompositeBackdrop(commands, blurred, in cmds[splits![s - 1]], ref active);
            }
            EncodeRange(displayList, commands, layers, start, end);
            if (segment is null) frame.Submit(commands);
            else commands.Submit(waitUntilCompleted: true);
            if (content is not null) _pool.Return(content);
            if (blurred is not null) _pool.Return(blurred);
            if (isLast) return;

            content = segment;
            blurred = Blur(segment!, cmds[splits![s]].StrokeWidth);
            start = splits[s] + 1;
        }
    }

    /// <summary>Draws the blurred backdrop clipped to the glass region — layer_composite at alpha 1
    /// with the command's device-space rrect riding the clip uniforms (AA edge for free).</summary>
    private static void CompositeBackdrop(IRhiCommandList commands, IRhiRenderTarget blurred,
        in DrawCommand command, ref RhiPipelineKind activeKind)
    {
        if (command.Clip is not { } region) return;
        if (activeKind != RhiPipelineKind.LayerComposite)
        {
            commands.SetPipeline(RhiPipelineKind.LayerComposite);
            activeKind = RhiPipelineKind.LayerComposite;
        }
        commands.BindTexture(ColorSlot, blurred);
        var uniforms = new DrawUniforms
        {
            Inv0 = new Float4(1, 0, 0, 1),
            Inv1 = new Float4(0, 1, 0, 0),
            Rect = new Float4(blurred.Width / 2f, blurred.Height / 2f, blurred.Width / 2f, blurred.Height / 2f),
            ColorA = new Float4(1, 1, 1, 1),
            Gradient = new Float4(blurred.Width, blurred.Height, 0, 0),
            Flags = new Float4(0, 0, 1, 0),
            ClipRect = new Float4(region.Rect.Center.X, region.Rect.Center.Y, region.Rect.Width / 2, region.Rect.Height / 2),
            ClipRadii = new Float4(region.Radii.TopLeft, region.Radii.TopRight, region.Radii.BottomRight, region.Radii.BottomLeft),
        };
        commands.Draw(in uniforms);
    }

    /// <summary>
    /// PHASE ONE of the frame: every GROUP-OPACITY layer scope is rendered into its own offscreen
    /// target BEFORE the parent pass opens, innermost first. Doing it up front — rather than
    /// nesting passes — is what keeps both backends happy: Metal and Vulkan each allow only one
    /// open render pass per encoder, and a nested-pass design would have needed different
    /// scaffolding on each. Returns null when the list has no layers (the common case allocates
    /// nothing).
    /// </summary>
    private LayerTargets? RenderLayers(DisplayList displayList, int width, int height)
    {
        var cmds = displayList.Commands;
        var hasLayer = false;
        for (var i = 0; i < cmds.Length && !hasLayer; i++)
            hasLayer = cmds[i].Kind == DrawCommandKind.BeginLayer;
        if (!hasLayer) return null;

        // Walk innermost-first by resolving each BeginLayer against its matching EndLayer and
        // recursing into the enclosed range.
        RenderLayerRange(displayList, 0, cmds.Length, width, height, _layers);
        return _layers;
    }

    private void RenderLayerRange(DisplayList displayList, int start, int end, int width, int height, LayerTargets layers)
    {
        var cmds = displayList.Commands;
        for (var i = start; i < end; i++)
        {
            if (cmds[i].Kind != DrawCommandKind.BeginLayer) continue;

            var depth = 0;
            var close = -1;
            for (var j = i; j < end; j++)
            {
                if (cmds[j].Kind == DrawCommandKind.BeginLayer) depth++;
                else if (cmds[j].Kind == DrawCommandKind.EndLayer && --depth == 0) { close = j; break; }
            }
            if (close < 0) return; // unbalanced: the builder refuses it, and the encoder fails on it

            // Nested layers inside this scope render FIRST, so this scope's own pass can composite
            // them while it draws.
            RenderLayerRange(displayList, i + 1, close, width, height, layers);

            var target = _pool.Rent(width, height);
            var commands = _device.Begin(target, Color.Transparent);
            EncodeRange(displayList, commands, layers, i + 1, close);
            commands.Submit(waitUntilCompleted: true);
            layers.Add(i, target);

            i = close; // this scope is done; continue after it
        }
    }

    /// <summary>The pooled targets a frame's layer scopes rendered into, keyed by the index of their
    /// BeginLayer command, given back to the pool when the frame is done.</summary>
    private sealed class LayerTargets
    {
        private readonly Dictionary<int, IRhiRenderTarget> _targets = new();

        public void Add(int beginIndex, IRhiRenderTarget target) => _targets[beginIndex] = target;

        public IRhiRenderTarget? Get(int beginIndex) => _targets.GetValueOrDefault(beginIndex);

        public void Return(RhiTargetPool pool)
        {
            foreach (var target in _targets.Values) pool.Return(target);
            _targets.Clear();
        }
    }

    private void EncodeRange(DisplayList displayList, IRhiCommandList commands, LayerTargets? layers,
        int start, int end)
    {
        commands.SetPipeline(RhiPipelineKind.Sdf);
        // The generated entry points share the texture bindings — keep every slot valid for every
        // pipeline (strict API validation) with a 1×1 dummy until a real texture binds.
        var dummy = Dummy();
        commands.BindTexture(CoverageSlot, dummy);
        commands.BindTexture(ColorSlot, dummy);
        var activeKind = RhiPipelineKind.Sdf;

        var cmds = displayList.Commands;
        for (var i = start; i < end; i++)
        {
            ref readonly var command = ref cmds[i];
            if (command.Kind == DrawCommandKind.Clear) continue; // leading clears only (spike fence)
            // Top-level backdrops were consumed by the pass split; one appearing here is inside a
            // layer scope — fenced (the layer is isolated from its backdrop), skipped like the CPU.
            if (command.Kind == DrawCommandKind.BackdropBlur) continue;
            if (command.Kind == DrawCommandKind.Texture)
            {
                if (command.TextureId < 0 || command.TextureId >= displayList.Textures.Count) continue;
                var data = displayList.Textures[command.TextureId];
                if (!DrawUniforms.TryBuild(in command, out var textured)) continue;
                // The textured path reuses free slots rather than growing the block: the texture
                // size rides `Gradient`, and a GRADIENT TINT (gradient text) rides `Radii` (axis)
                // + `ColorB` (second stop) — neither is read by the textured entry points.
                // WHICH run, not merely whether: 1 linear, 2 radial. A radial used to arrive as a
                // zero here and paint flat in its first stop — invisible on text, which only ever
                // asked for linear, and immediately visible the day artwork brought a radial in.
                var gradientTint = command.Paint.Kind switch
                {
                    PaintKind.LinearGradient => 1f,
                    PaintKind.RadialGradient => 2f,
                    _ => 0f,
                };
                if (gradientTint > 0)
                {
                    // Linear: the axis ends. Radial: the centre and the two radii — the same four
                    // floats reinterpreted, exactly as Paint does.
                    textured.Radii = new Float4(
                        command.Paint.GradientStart.X, command.Paint.GradientStart.Y,
                        command.Paint.GradientEnd.X, command.Paint.GradientEnd.Y);
                }
                textured.Gradient = new Float4(data.Width, data.Height, 0, 0);
                textured.Flags = new Float4(0, gradientTint, command.Clip is null ? 0 : 1, 0);

                var kind = data.Format == TextureFormat.Rgba8 ? RhiPipelineKind.TexturedRgba : RhiPipelineKind.TexturedA8;
                if (activeKind != kind)
                {
                    commands.SetPipeline(kind);
                    activeKind = kind;
                }
                commands.BindTexture(kind == RhiPipelineKind.TexturedRgba ? ColorSlot : CoverageSlot, TextureFor(data));
                commands.Draw(in textured);
                continue;
            }
            if (command.Kind == DrawCommandKind.TiledTexture)
            {
                if (command.TextureId < 0 || command.TextureId >= displayList.Textures.Count) continue;
                var tile = displayList.Textures[command.TextureId];
                if (!DrawUniforms.TryBuildTiled(in command, tile, out var tiled)) continue;
                if (activeKind != RhiPipelineKind.TiledRgba)
                {
                    commands.SetPipeline(RhiPipelineKind.TiledRgba);
                    activeKind = RhiPipelineKind.TiledRgba;
                }
                commands.BindTexture(ColorSlot, TextureFor(tile));
                commands.Draw(in tiled);
                continue;
            }
            if (activeKind != RhiPipelineKind.Sdf)
            {
                commands.SetPipeline(RhiPipelineKind.Sdf);
                activeKind = RhiPipelineKind.Sdf;
            }
            if (command.Kind == DrawCommandKind.BeginLayer)
            {
                // Phase one rendered this scope into its own target, so the whole group composites
                // with ONE blend and overlapping children never double-blend. Skip to the matching
                // EndLayer — its contents are already drawn. Every way into the encoder runs phase
                // one, so a scope without a target is a list no builder makes, and it fails here
                // rather than drawing each child at the group's alpha, as the windows once did.
                var layerTarget = layers?.Get(i)
                    ?? throw new InvalidOperationException(
                        $"The layer that begins at command {i} has no target: its scope is not balanced.");
                CompositeLayer(commands, layerTarget, command.StrokeWidth, ref activeKind);
                i = MatchingEnd(cmds, i, end);
                continue;
            }
            // An EndLayer is reached only past its BeginLayer, which skips it; one alone closes nothing.
            if (command.Kind == DrawCommandKind.EndLayer) continue;
            if (!DrawUniforms.TryBuild(in command, out var uniforms)) continue;
            commands.Draw(in uniforms);
        }
    }

    /// <summary>
    /// Runs the dual-Kawase pyramid over <paramref name="source"/> and returns a full-size target
    /// from the pool with the blurred result, which the frame gives back once a pass has read it; the
    /// levels go back here. Each level is one fullscreen draw through the LINEAR sampler (D6b); the
    /// tap pattern is Blur.cs's, which is the normative math. v1 submits level-by-level with waits —
    /// correct first; folding the pyramid into one command buffer is a known optimization for the
    /// frame path.
    /// </summary>
    internal IRhiRenderTarget Blur(IRhiRenderTarget source, float radius)
    {
        var levels = Engine.Blur.Levels(radius);
        var down = new IRhiRenderTarget[levels];
        IRhiTexture current = source;
        for (var i = 0; i < levels; i++)
        {
            var width = Math.Max(1, source.Width >> (i + 1));
            var height = Math.Max(1, source.Height >> (i + 1));
            down[i] = _pool.Rent(width, height);
            BlurPass(current, down[i], RhiPipelineKind.BlurDown);
            current = down[i];
        }

        // Coming back up, each finished DOWN target above the current level is reused as the up
        // destination — its contents were already consumed by the level below.
        for (var i = levels - 2; i >= 0; i--)
        {
            BlurPass(current, down[i], RhiPipelineKind.BlurUp);
            current = down[i];
        }

        var result = _pool.Rent(source.Width, source.Height);
        BlurPass(current, result, RhiPipelineKind.BlurUp);
        foreach (var target in down) _pool.Return(target);
        return result;
    }

    private void BlurPass(IRhiTexture source, IRhiRenderTarget destination, RhiPipelineKind kind)
    {
        var commands = _device.Begin(destination, Color.Transparent);
        commands.SetPipeline(kind);
        // The dummy binds FIRST: the sampler slot is global, so the filtered source must be the
        // LAST bind or a later nearest bind would override the linear filter.
        commands.BindTexture(CoverageSlot, Dummy());
        commands.BindTexture(ColorSlot, source, RhiFilter.Linear);

        var uniforms = new DrawUniforms
        {
            Inv0 = new Float4(1, 0, 0, 1),
            Inv1 = new Float4(0, 1, 0, 0),
            Rect = new Float4(destination.Width / 2f, destination.Height / 2f,
                destination.Width / 2f, destination.Height / 2f),
            // Half a DESTINATION texel, in UV — precomputed so the shader never divides.
            Gradient = new Float4(0.5f / destination.Width, 0.5f / destination.Height, 0, 0),
        };
        commands.Draw(in uniforms);
        commands.Submit(waitUntilCompleted: true);
    }

    /// <summary>Index of the EndLayer matching the BeginLayer at <paramref name="begin"/>.</summary>
    private static int MatchingEnd(ReadOnlySpan<DrawCommand> cmds, int begin, int end)
    {
        var depth = 0;
        for (var j = begin; j < end; j++)
        {
            if (cmds[j].Kind == DrawCommandKind.BeginLayer) depth++;
            else if (cmds[j].Kind == DrawCommandKind.EndLayer && --depth == 0) return j;
        }
        return end - 1;
    }

    /// <summary>
    /// Draws a rendered layer target over the current pass at <paramref name="alpha"/>. The target
    /// holds PREMULTIPLIED texels, so the composite pipeline scales rather than premultiplies —
    /// reusing the image pipeline here would apply alpha twice.
    /// </summary>
    private static void CompositeLayer(IRhiCommandList commands, IRhiRenderTarget layer, float alpha,
        ref RhiPipelineKind activeKind)
    {
        if (activeKind != RhiPipelineKind.LayerComposite)
        {
            commands.SetPipeline(RhiPipelineKind.LayerComposite);
            activeKind = RhiPipelineKind.LayerComposite;
        }
        commands.BindTexture(ColorSlot, layer);

        // The layer covers the whole surface in DEVICE space: identity transform, full-extent rect.
        var uniforms = new DrawUniforms
        {
            Inv0 = new Float4(1, 0, 0, 1),
            Inv1 = new Float4(0, 1, 0, 0),
            Rect = new Float4(layer.Width / 2f, layer.Height / 2f, layer.Width / 2f, layer.Height / 2f),
            ColorA = new Float4(1, 1, 1, alpha),
            Gradient = new Float4(layer.Width, layer.Height, 0, 0),
            Flags = new Float4(0, 0, 0, 0),
        };
        commands.Draw(in uniforms);
    }

    /// <summary>Uploads (and caches by IDENTITY) a display-list raster. An entry lives while frames
    /// keep drawing it (see <see cref="RetainFrames"/>) — the host's raster caches reuse instances
    /// across frames, so what is on screen stays warm.
    /// A LIVE texture (camera) mutates its bytes in place and bumps <see cref="TextureData.Version"/>:
    /// same instance, same cache slot, re-uploaded when the number moved and the upload it replaces
    /// released — which is what keeps a 30 fps feed at exactly ONE GPU texture.</summary>
    private IRhiTexture TextureFor(TextureData data)
    {
        if (_textures.TryGetValue(data, out var uploaded))
        {
            uploaded.LastFrame = _frame;
            if (uploaded.Version == data.Version) return uploaded.Texture;
            uploaded.Texture.Dispose();
            uploaded.Texture = _device.CreateTexture(data);
            uploaded.Version = data.Version;
            return uploaded.Texture;
        }
        var texture = _device.CreateTexture(data);
        _textures[data] = new Uploaded(texture, data.Version, _frame);
        _uploads++;
        return texture;
    }

    /// <summary>
    /// A new frame of <paramref name="width"/> by <paramref name="height"/> starts: the rasters that
    /// went undrawn for more than <see cref="RetainFrames"/> whole frames are released, before
    /// anything is encoded, so nothing this frame binds can be one of them, and so are the pooled
    /// targets this frame cannot use (see <see cref="RhiTargetPool"/>). A raster drawn again later is
    /// uploaded again, from the bytes its owner still holds.
    /// </summary>
    private void BeginFrame(int width, int height)
    {
        _frame++;
        _pool.BeginFrame(_frame, width, height);
        // As many as the last frame uploaded, and never fewer than the share: see ReleasesPerFrame.
        var releases = Math.Max(ReleasesPerFrame, _uploads);
        _uploads = 0;
        // The frame that is starting has drawn nothing yet, so it is not one of the frames a raster
        // "went undrawn" for: one drawn at frame f was away for (_frame - f - 1) frames.
        foreach (var entry in _textures)
        {
            if (_frame - entry.Value.LastFrame - 1 <= RetainFrames) continue;
            _retired.Add(entry.Key);
            if (_retired.Count == releases) break;
        }
        if (_retired.Count == 0) return;

        try
        {
            // Out of the map first: a release that throws must not leave an entry whose texture
            // is gone, nor a list of keys the next frame would trip over.
            foreach (var data in _retired)
                if (_textures.Remove(data, out var uploaded)) uploaded.Texture.Dispose();
        }
        finally
        {
            _retired.Clear();
        }
    }

    private IRhiTexture Dummy() => _dummy ??= _device.CreateTexture(new TextureData(1, 1, new byte[] { 0 }));

    public void Dispose()
    {
        foreach (var uploaded in _textures.Values) uploaded.Texture.Dispose();
        _textures.Clear();
        _pool.Dispose();
        _dummy?.Dispose();
        _dummy = null;
    }
}

/// <summary>The generic <see cref="IRenderSurface"/> over an RHI render target — GPU backends'
/// CreateSurface returns this; only the target underneath is backend-specific.</summary>
public sealed class RhiSurface : IRenderSurface
{
    public RhiSurface(IRhiRenderTarget target) => Target = target;

    public IRhiRenderTarget Target { get; }

    public int Width => Target.Width;
    public int Height => Target.Height;

    public void ReadPixelsSrgb(Span<byte> destinationRgba) => Target.ReadPixelsSrgb(destinationRgba);

    public void Dispose() => Target.Dispose();
}

/// <summary>
/// The shared readback conversion: premultiplied sRGB-encoded RGBA bytes (what a GPU render
/// target stores after blending) → the straight-alpha golden interchange format. sRGB-decode the
/// premultiplied channels (alpha stays linear in <c>*_sRGB</c> formats), un-premultiply in linear
/// space, re-encode — ReferenceSurface's output math after an 8-bit round trip. Both GPU backends
/// funnel through here so their readbacks can only agree.
/// </summary>
public static class RhiReadback
{
    public static void UnpremultiplySrgb(ReadOnlySpan<byte> premultipliedRgba, Span<byte> destinationRgba)
    {
        for (var i = 0; i < premultipliedRgba.Length; i += 4)
        {
            var a8 = premultipliedRgba[i + 3];
            if (a8 == 0)
            {
                destinationRgba[i] = destinationRgba[i + 1] = destinationRgba[i + 2] = destinationRgba[i + 3] = 0;
                continue;
            }

            var invA = 255f / a8;
            destinationRgba[i + 0] = ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear(premultipliedRgba[i + 0]) * invA);
            destinationRgba[i + 1] = ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear(premultipliedRgba[i + 1]) * invA);
            destinationRgba[i + 2] = ColorSpace.LinearToSrgb(ColorSpace.SrgbToLinear(premultipliedRgba[i + 2]) * invA);
            destinationRgba[i + 3] = a8;
        }
    }
}
