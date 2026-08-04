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
            command.Kind switch { DrawCommandKind.StrokeRRect => 1, DrawCommandKind.ShadowRRect => 2, _ => 0 },
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

    private static Float4 ToFloat4(Color color) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
}

/// <summary>A GPU texture the encoder can bind for sampling (A8 coverage or straight-sRGB RGBA).</summary>
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
/// every GPU backend runs the identical frame semantics: leading Clear becomes the pass clear,
/// group-opacity layers approximate as per-command alpha (documented fence — the D4 offscreen
/// composite pass replaces this), Texture commands switch to the textured pipelines, and each
/// command becomes one uniform block + one fullscreen-triangle draw. Owns the per-device sampled-
/// texture cache (keyed by <see cref="TextureData"/> IDENTITY — the raster caches reuse instances).
/// </summary>
public sealed class RhiRenderer : IDisposable
{
    /// <summary>Texture slot for A8 coverage rasters (<c>coverageTexture</c>, binding 1).</summary>
    public const int CoverageSlot = 0;

    /// <summary>Texture slot for straight-sRGB RGBA images (<c>colorTexture</c>, binding 2).</summary>
    public const int ColorSlot = 1;

    private readonly IRhiDevice _device;
    private readonly Dictionary<TextureData, (IRhiTexture Texture, int Version)> _textures = new();
    private IRhiTexture? _dummy;

    public RhiRenderer(IRhiDevice device) => _device = device;

    /// <summary>
    /// The pass clear for <paramref name="displayList"/>: its leading Clear command's color
    /// (engine lists always start with one), or transparent. Callers pass this to
    /// <see cref="IRhiDevice.Begin"/>; <see cref="Encode"/> then skips Clear commands.
    /// </summary>
    public static Color ClearColorOf(DisplayList displayList)
    {
        var commands = displayList.Commands;
        return commands.Length > 0 && commands[0].Kind == DrawCommandKind.Clear
            ? commands[0].Paint.Color
            : Color.Transparent;
    }

    /// <summary>Offscreen path: begin, encode, submit-and-wait (the caller reads pixels right after).</summary>
    public void Render(DisplayList displayList, IRhiRenderTarget target)
    {
        var layers = RenderLayers(displayList, target.Width, target.Height);
        var splits = FindTopLevelBackdrops(displayList.Commands);
        if (splits is null)
        {
            var commands = _device.Begin(target, ClearColorOf(displayList));
            Encode(displayList, commands, layers);
            commands.Submit(waitUntilCompleted: true);
            layers?.Dispose();
            return;
        }
        RenderWithBackdrops(displayList, target, layers, splits);
        layers?.Dispose();
    }

    /// <summary>Indices of BackdropBlur commands OUTSIDE layer scopes (the fence: inside a
    /// group-opacity layer the backdrop is isolated and the command is skipped). Null when none —
    /// the common frame takes the single-pass path untouched.</summary>
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
    /// The frosted-glass frame (W3): each BackdropBlur SPLITS the frame into passes — the content
    /// so far accumulates offscreen, runs the pyramid, and the next pass seeds itself by
    /// compositing that content plus the blurred copy clipped to the region rrect, then keeps
    /// drawing. The LAST segment draws straight into the caller's target. Metal and Vulkan allow
    /// one open pass per encoder, so "read what you just drew" is necessarily a pass boundary.
    /// </summary>
    private void RenderWithBackdrops(DisplayList displayList, IRhiRenderTarget target,
        object? layersHandle, List<int> splits)
    {
        var layers = layersHandle as LayerTargets;
        var cmds = displayList.Commands;
        var width = target.Width;
        var height = target.Height;

        IRhiRenderTarget? content = null;  // the accumulated frame BELOW the current split
        IRhiRenderTarget? blurred = null;  // its pyramid output
        var start = 0;
        for (var s = 0; s <= splits.Count; s++)
        {
            var isLast = s == splits.Count;
            var end = isLast ? cmds.Length : splits[s];
            var destination = isLast ? target : _device.CreateRenderTarget(width, height);
            var commands = _device.Begin(destination, s == 0 ? ClearColorOf(displayList) : Color.Transparent);
            if (content is not null && blurred is not null)
            {
                // Seed: full content below, then the blurred backdrop inside the glass rrect.
                commands.SetPipeline(RhiPipelineKind.Sdf);
                commands.BindTexture(CoverageSlot, Dummy());
                commands.BindTexture(ColorSlot, Dummy());
                var active = RhiPipelineKind.Sdf;
                CompositeLayer(commands, content, 1f, ref active);
                CompositeBackdrop(commands, blurred, in cmds[splits[s - 1]], ref active);
            }
            EncodeRange(displayList, commands, layers, start, end);
            commands.Submit(waitUntilCompleted: true);
            content?.Dispose();
            blurred?.Dispose();
            if (isLast) return;

            content = destination;
            blurred = Blur(content, cmds[splits[s]].StrokeWidth);
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

        var layers = new LayerTargets();
        // Walk innermost-first by resolving each BeginLayer against its matching EndLayer and
        // recursing into the enclosed range.
        RenderLayerRange(displayList, 0, cmds.Length, width, height, layers);
        return layers;
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
            if (close < 0) return; // unbalanced (the builder rejects this; be defensive anyway)

            // Nested layers inside this scope render FIRST, so this scope's own pass can composite
            // them while it draws.
            RenderLayerRange(displayList, i + 1, close, width, height, layers);

            var target = _device.CreateRenderTarget(width, height);
            var commands = _device.Begin(target, Color.Transparent);
            EncodeRange(displayList, commands, layers, i + 1, close);
            commands.Submit(waitUntilCompleted: true);
            layers.Add(i, target);

            i = close; // this scope is done; continue after it
        }
    }

    /// <summary>The offscreen targets a frame's layer scopes rendered into, keyed by the index of
    /// their BeginLayer command. Disposed with the frame.</summary>
    private sealed class LayerTargets : IDisposable
    {
        private readonly Dictionary<int, IRhiRenderTarget> _targets = new();

        public void Add(int beginIndex, IRhiRenderTarget target) => _targets[beginIndex] = target;

        public IRhiRenderTarget? Get(int beginIndex) => _targets.GetValueOrDefault(beginIndex);

        public void Dispose()
        {
            foreach (var target in _targets.Values) target.Dispose();
            _targets.Clear();
        }
    }

    /// <summary>
    /// Encodes <paramref name="displayList"/> into an open pass. Window paths call this directly
    /// with a backend-created command list (drawable target) and submit without waiting.
    /// </summary>
    public void Encode(DisplayList displayList, IRhiCommandList commands) =>
        Encode(displayList, commands, layersHandle: null);

    /// <summary>
    /// Encodes <paramref name="displayList"/> into an open pass, compositing any layer scopes from
    /// the offscreen targets phase one produced. A null <paramref name="layers"/> means the caller
    /// never ran phase one — layers then fall back to the historical per-command alpha
    /// approximation, which is what the WINDOW path used before offscreen layers existed.
    /// </summary>
    public void Encode(DisplayList displayList, IRhiCommandList commands, object? layersHandle)
    {
        var layers = layersHandle as LayerTargets;
        EncodeRange(displayList, commands, layers, 0, displayList.Commands.Length);
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

        // FENCE (carried from the spike): group-opacity layers approximate as per-command alpha —
        // overlapping children inside a layer double-blend; the Reference backend is normative and
        // the bounded offscreen composite pass (plan D4) retires this.
        var layerAlpha = 1f;
        Stack<float>? layerStack = null;

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
                if (layerAlpha < 1f)
                {
                    textured.ColorA.W *= layerAlpha;
                    textured.ColorB.W *= layerAlpha;
                }
                // The textured path reuses free slots rather than growing the block: the texture
                // size rides `Gradient`, and a GRADIENT TINT (gradient text) rides `Radii` (axis)
                // + `ColorB` (second stop) — neither is read by the textured entry points.
                var gradientTint = command.Paint.Kind == PaintKind.LinearGradient;
                if (gradientTint)
                {
                    textured.Radii = new Float4(
                        command.Paint.GradientStart.X, command.Paint.GradientStart.Y,
                        command.Paint.GradientEnd.X, command.Paint.GradientEnd.Y);
                }
                textured.Gradient = new Float4(data.Width, data.Height, 0, 0);
                textured.Flags = new Float4(0, gradientTint ? 1 : 0, command.Clip is null ? 0 : 1, 0);

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
            if (activeKind != RhiPipelineKind.Sdf)
            {
                commands.SetPipeline(RhiPipelineKind.Sdf);
                activeKind = RhiPipelineKind.Sdf;
            }
            if (command.Kind == DrawCommandKind.BeginLayer)
            {
                // REAL offscreen layer: phase one rendered this scope into its own target, so the
                // whole group composites with ONE blend and overlapping children never
                // double-blend. Skip to the matching EndLayer — its contents are already drawn.
                if (layers?.Get(i) is { } layerTarget)
                {
                    CompositeLayer(commands, layerTarget, command.StrokeWidth * layerAlpha, ref activeKind);
                    i = MatchingEnd(cmds, i, end);
                    continue;
                }
                // Fallback (window path, pre-offscreen): approximate as per-command alpha —
                // overlapping children double-blend. Documented, and now only reachable when the
                // caller skipped phase one.
                (layerStack ??= new()).Push(layerAlpha);
                layerAlpha *= command.StrokeWidth;
                continue;
            }
            if (command.Kind == DrawCommandKind.EndLayer)
            {
                if (layerStack is { Count: > 0 }) layerAlpha = layerStack.Pop();
                continue;
            }
            if (!DrawUniforms.TryBuild(in command, out var uniforms)) continue;
            if (layerAlpha < 1f)
            {
                uniforms.ColorA.W *= layerAlpha;
                uniforms.ColorB.W *= layerAlpha;
            }
            commands.Draw(in uniforms);
        }
    }

    /// <summary>
    /// Runs the dual-Kawase pyramid over <paramref name="source"/> and returns a NEW full-size
    /// target with the blurred result (caller disposes it; intermediates are disposed here). Each
    /// level is one fullscreen draw through the LINEAR sampler (D6b); the tap pattern is Blur.cs's,
    /// which is the normative math. v1 submits level-by-level with waits — correct first; folding
    /// the pyramid into one command buffer is a known optimization for the frame path.
    /// </summary>
    public IRhiRenderTarget Blur(IRhiRenderTarget source, float radius)
    {
        var levels = Engine.Blur.Levels(radius);
        var down = new IRhiRenderTarget[levels];
        IRhiTexture current = source;
        for (var i = 0; i < levels; i++)
        {
            var width = Math.Max(1, source.Width >> (i + 1));
            var height = Math.Max(1, source.Height >> (i + 1));
            down[i] = _device.CreateRenderTarget(width, height);
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

        var result = _device.CreateRenderTarget(source.Width, source.Height);
        BlurPass(current, result, RhiPipelineKind.BlurUp);
        foreach (var target in down) target.Dispose();
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

    /// <summary>Uploads (and caches by IDENTITY) a display-list raster. Cache lifetime is the
    /// renderer's — the host's raster caches reuse instances across frames, so entries stay warm.
    /// A LIVE texture (camera) mutates its bytes in place and bumps <see cref="TextureData.Version"/>:
    /// same instance, same cache slot, re-uploaded when the number moved — which is what keeps a
    /// 30 fps feed at exactly ONE GPU texture instead of a new leaked one per frame.</summary>
    private IRhiTexture TextureFor(TextureData data)
    {
        if (_textures.TryGetValue(data, out var cached))
        {
            if (cached.Version == data.Version) return cached.Texture;
            cached.Texture.Dispose();
        }
        var texture = _device.CreateTexture(data);
        _textures[data] = (texture, data.Version);
        return texture;
    }

    private IRhiTexture Dummy() => _dummy ??= _device.CreateTexture(new TextureData(1, 1, new byte[] { 0 }));

    public void Dispose()
    {
        foreach (var entry in _textures.Values) entry.Texture.Dispose();
        _textures.Clear();
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
