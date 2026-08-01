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
    /// the normative shader's binding order after the uniform block.
    /// </summary>
    void BindTexture(int slot, IRhiTexture texture);

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
    private readonly Dictionary<TextureData, IRhiTexture> _textures = new();
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
        var commands = _device.Begin(target, ClearColorOf(displayList));
        Encode(displayList, commands);
        commands.Submit(waitUntilCompleted: true);
    }

    /// <summary>
    /// Encodes <paramref name="displayList"/> into an open pass. Window paths call this directly
    /// with a backend-created command list (drawable target) and submit without waiting.
    /// </summary>
    public void Encode(DisplayList displayList, IRhiCommandList commands)
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
        for (var i = 0; i < cmds.Length; i++)
        {
            ref readonly var command = ref cmds[i];
            if (command.Kind == DrawCommandKind.Clear) continue; // leading clears only (spike fence)
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
                (layerStack ??= new()).Push(layerAlpha);
                layerAlpha *= command.StrokeWidth;
                continue;
            }
            if (command.Kind == DrawCommandKind.EndLayer)
            {
                layerAlpha = layerStack!.Pop();
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

    /// <summary>Uploads (and caches by IDENTITY) a display-list raster. Cache lifetime is the
    /// renderer's — the host's raster caches reuse instances across frames, so entries stay warm.</summary>
    private IRhiTexture TextureFor(TextureData data)
    {
        if (_textures.TryGetValue(data, out var cached)) return cached;
        var texture = _device.CreateTexture(data);
        _textures[data] = texture;
        return texture;
    }

    private IRhiTexture Dummy() => _dummy ??= _device.CreateTexture(new TextureData(1, 1, new byte[] { 0 }));

    public void Dispose()
    {
        foreach (var texture in _textures.Values) texture.Dispose();
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
