using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using eQuantic.UI.Native.Engine.Reference;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>W4: the Texture command on the NORMATIVE Reference backend — A8 coverage × tint,
/// nearest sampling (rasters are device-scale, texels map 1:1), identity dedupe in the builder.</summary>
public class TextureCommandTests
{
    [Fact]
    public void Texture_DrawsCoverageTintedByThePaint()
    {
        // 2×1: left texel opaque, right transparent.
        var texture = new TextureData(2, 1, new byte[] { 255, 0 });
        var builder = new DisplayListBuilder();
        builder.Clear(new Color(0, 0, 0, 255));
        builder.Texture(new Rect(0, 0, 2, 1), new Color(255, 0, 0, 255), texture);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(2, 1);
        backend.Render(builder.Build(), surface);

        var rgba = new byte[2 * 1 * 4];
        surface.ReadPixelsSrgb(rgba);
        rgba[0].Should().BeGreaterThan(200, "the covered texel draws the tint");
        rgba[4].Should().Be(0, "the transparent texel leaves the clear color");
    }

    [Fact]
    public void RegisterTexture_DedupesByIdentity()
    {
        var texture = new TextureData(1, 1, new byte[] { 255 });
        var builder = new DisplayListBuilder();
        builder.Clear(new Color(0, 0, 0, 255));
        builder.Texture(new Rect(0, 0, 1, 1), new Color(255, 255, 255, 255), texture);
        builder.Texture(new Rect(0, 0, 1, 1), new Color(0, 255, 0, 255), texture);

        builder.Build().Textures.Should().HaveCount(1, "the cache hands the same instance every frame");
    }
}
