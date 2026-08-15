using eQuantic.UI.Native.Engine.Metal;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>One Metal device + compiled pipeline shared across the parity cases (created lazily so unsupported hosts skip, not fail).</summary>
public sealed class MetalFixture : IDisposable
{
    private readonly Lazy<MetalBackend> _backend = new(() => new MetalBackend());

    public bool IsSupported => MetalBackend.IsSupported;
    public MetalBackend Backend => _backend.Value;

    public void Dispose()
    {
        if (_backend.IsValueCreated) _backend.Value.Dispose();
    }
}

/// <summary>
/// The M0 exit gate for the GPU track: every <see cref="GoldenScenes"/> scene rendered by the Metal
/// backend must match the Reference backend within a fuzzy tolerance. The comparison is Metal-vs-
/// Reference (not stored goldens): the Reference is the normative pixel source, and the tolerance
/// absorbs only GPU-vs-CPU representation noise — the 8-bit sRGB intermediate quantization on
/// blended overlaps, fast-math <c>pow</c> in the shader's sRGB decode, and the hardware's sRGB
/// encode-on-store — never geometry. A transliteration bug (wrong corner select, wrong stroke band,
/// wrong blend factors) moves whole edge runs by dozens of values and fails both gates immediately.
/// Skips (never fails) on hosts without a Metal device.
/// </summary>
public class MetalParityTests : IClassFixture<MetalFixture>
{
    private readonly MetalFixture _metal;

    public MetalParityTests(MetalFixture metal) => _metal = metal;

    [SkippableTheory]
    [MemberData(nameof(Scenes))]
    public void MatchesReference(string name)
    {
        Skip.IfNot(_metal.IsSupported, "No Metal device on this host — GPU parity runs on Apple hardware only.");

        var displayList = GoldenScenes.Build(name);
        using var referenceBackend = new ReferenceBackend();
        var referencePixels = Render(referenceBackend, displayList);
        var metalPixels = Render(_metal.Backend, displayList);

        GpuParity.AssertFuzzyEqual(
            "Metal", name, GoldenScenes.Width, GoldenScenes.Height, referencePixels, metalPixels);
    }

    [SkippableFact]
    public void Backend_ReportsMetalKind_AndSurfaceDimensions()
    {
        Skip.IfNot(_metal.IsSupported, "No Metal device on this host.");

        Assert.Equal(RenderBackendKind.Metal, _metal.Backend.Kind);
        using var surface = _metal.Backend.CreateSurface(64, 32);
        Assert.Equal(64, surface.Width);
        Assert.Equal(32, surface.Height);
    }

    public static TheoryData<string> Scenes => GoldenScenes.Names;

    private static byte[] Render(IRenderBackend backend, DisplayList displayList)
    {
        using var surface = backend.CreateSurface(GoldenScenes.Width, GoldenScenes.Height);
        backend.Render(displayList, surface);
        var pixels = new byte[GoldenScenes.Width * GoldenScenes.Height * 4];
        surface.ReadPixelsSrgb(pixels);
        return pixels;
    }

}
