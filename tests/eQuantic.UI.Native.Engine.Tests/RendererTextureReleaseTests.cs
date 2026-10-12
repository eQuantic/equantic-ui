using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the shared renderer keeps uploaded follows what is DRAWN (#858). It kept every raster it
/// ever uploaded for its own life, and on Android an app that drew a thousand different texts ran
/// out of Vulkan descriptor sets and died: a stopwatch, a counter, a frame counter beside a camera
/// preview. The device here only counts, which is all the defect needed to be seen.
/// </summary>
public class RendererTextureReleaseTests
{
    private sealed class CountingDevice : IRhiDevice
    {
        public int Created { get; private set; }
        public int Live { get; private set; }
        public Dictionary<TextureData, int> Uploads { get; } = new();

        public IRhiTexture CreateTexture(TextureData data)
        {
            Created++;
            Live++;
            Uploads[data] = Uploads.GetValueOrDefault(data) + 1;
            return new CountedTexture(this, data);
        }

        public IRhiRenderTarget CreateRenderTarget(int width, int height) => throw new NotSupportedException();
        public IRhiCommandList Begin(IRhiRenderTarget target, Color clearColor) => throw new NotSupportedException();
        public void Dispose() { }

        internal sealed class CountedTexture(CountingDevice device, TextureData data) : IRhiTexture
        {
            public bool Released { get; private set; }
            public int Width => data.Width;
            public int Height => data.Height;

            public void Dispose()
            {
                Released.Should().BeFalse("a texture is released once");
                Released = true;
                device.Live--;
            }
        }
    }

    /// <summary>A pass that refuses a texture already released: what a frame binds is still on the GPU.</summary>
    private sealed class Pass : IRhiCommandList
    {
        public int Bound { get; private set; }

        public void SetPipeline(RhiPipelineKind kind) { }

        public void BindTexture(int slot, IRhiTexture texture, RhiFilter filter = RhiFilter.Nearest)
        {
            ((CountingDevice.CountedTexture)texture).Released.Should().BeFalse("a frame never binds a released texture");
            Bound++;
        }

        public void Draw(in DrawUniforms uniforms) { }
        public void Submit(bool waitUntilCompleted) { }
    }

    private static TextureData Raster() => new(2, 2, new byte[4]);

    private static DisplayList Frame(params TextureData[] rasters)
    {
        var builder = new DisplayListBuilder();
        foreach (var raster in rasters) builder.Texture(new Rect(0, 0, 2, 2), Color.White, raster);
        return builder.Build();
    }

    /// <summary>
    /// The measured case: a text that changes every frame is a new raster every frame. A thousand
    /// frames later the renderer holds the last few, not a thousand.
    /// </summary>
    [Fact]
    public void AThousandDifferentRasters_OneAFrame_AreNotAllHeld()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);

        for (var frame = 0; frame < 1000; frame++) renderer.Encode(Frame(Raster()), new Pass());

        device.Created.Should().BeGreaterThanOrEqualTo(1000, "each new raster is uploaded");
        device.Live.Should().BeLessThanOrEqualTo(RhiRenderer.RetainFrames + 3,
            "a raster no frame draws any more gives its texture back; kept, the 1,025th ended an Android app");
    }

    /// <summary>What stays on screen stays uploaded: releasing is for what left, never for what is drawn.</summary>
    [Fact]
    public void ARasterDrawnEveryFrame_IsUploadedOnce()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);
        var steady = Raster();

        for (var frame = 0; frame < 300; frame++) renderer.Encode(Frame(steady, Raster()), new Pass());

        device.Uploads[steady].Should().Be(1, "it was drawn in every frame, so it was never released");
    }

    /// <summary>A raster that left for longer than the renderer keeps it, and came back, is uploaded
    /// again from the bytes its owner still holds; one that came back sooner never left the GPU.</summary>
    [Theory]
    [InlineData(5, 1)]
    [InlineData(RhiRenderer.RetainFrames - 1, 1)]
    [InlineData(RhiRenderer.RetainFrames, 1)]        // away for exactly as long as it is kept: still there
    [InlineData(RhiRenderer.RetainFrames + 1, 2)]    // one frame longer: released, then uploaded again
    [InlineData(RhiRenderer.RetainFrames + 5, 2)]
    public void ARasterThatComesBack_IsUploadedAgainOnlyIfItWasReleased(int framesAway, int uploads)
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);
        var raster = Raster();

        renderer.Encode(Frame(raster), new Pass());
        for (var frame = 0; frame < framesAway; frame++) renderer.Encode(Frame(), new Pass());
        var back = new Pass();
        renderer.Encode(Frame(raster), back);

        device.Uploads[raster].Should().Be(uploads);
        back.Bound.Should().BeGreaterThan(0, "the frame that draws it again binds a texture that is on the GPU");
    }

    /// <summary>
    /// Every way into the encoder is a frame. The three-argument overload is the window path's own
    /// fallback; left out of the count, a caller on it would never release anything.
    /// </summary>
    [Fact]
    public void TheOverloadThatTakesLayers_CountsFramesToo()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);

        for (var frame = 0; frame < 500; frame++) renderer.Encode(Frame(Raster()), new Pass(), layersHandle: null);

        device.Live.Should().BeLessThanOrEqualTo(RhiRenderer.RetainFrames + 3);
    }

    /// <summary>
    /// Leaving a screen retires all of its rasters at once. They go a bounded number per frame, so
    /// the frame after is not the one that pays for the whole screen, and they all go.
    /// </summary>
    [Fact]
    public void AScreenLeft_IsReleasedAFewRastersAFrameUntilNoneIsHeld()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);
        var screen = Enumerable.Range(0, RhiRenderer.ReleasesPerFrame * 3 + 10).Select(_ => Raster()).ToArray();
        renderer.Encode(Frame(screen), new Pass());
        var held = device.Live;

        // A frame learns what it draws only as it encodes, so a raster is counted away at the start
        // of the frame AFTER one that left it out: the frame below is the first to find the screen
        // away for longer than it is kept.
        for (var frame = 0; frame <= RhiRenderer.RetainFrames; frame++) renderer.Encode(Frame(), new Pass());
        device.Live.Should().Be(held, "not yet away for longer than it is kept");

        renderer.Encode(Frame(), new Pass());
        device.Live.Should().Be(held - RhiRenderer.ReleasesPerFrame, "the first frame releases its share and no more");

        for (var frame = 0; frame < 4; frame++) renderer.Encode(Frame(), new Pass());
        device.Live.Should().Be(held - screen.Length, "and a few frames later none of the screen is held");
    }

    /// <summary>A live texture (a camera's frame) is one slot whose version moves: uploaded again per
    /// version, never more than one at a time.</summary>
    [Fact]
    public void ALiveRaster_HoldsOneTextureWhateverItsVersion()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);
        var live = Raster();
        renderer.Encode(Frame(live), new Pass());
        var held = device.Live;

        for (var version = 1; version <= 200; version++)
        {
            live.Version = version;
            renderer.Encode(Frame(live), new Pass());
            device.Live.Should().Be(held, "the old upload goes as the new one arrives");
        }
        device.Uploads[live].Should().Be(201);
    }

    [Fact]
    public void DisposingTheRenderer_ReleasesWhatItStillHolds()
    {
        var device = new CountingDevice();
        var renderer = new RhiRenderer(device);
        renderer.Encode(Frame(Raster(), Raster()), new Pass());

        renderer.Dispose();

        device.Live.Should().Be(0);
    }
}
