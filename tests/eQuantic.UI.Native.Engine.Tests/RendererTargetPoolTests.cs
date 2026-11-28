using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The targets a frame draws its own passes into, a group's layer, a backdrop's content and the
/// levels of its blur, come from a pool the renderer keeps across frames (#1952). They were created
/// at the surface's size and disposed in every frame: 20.7 MB of allocation a frame for one layer on
/// a 2880 by 1800 window. The device here only counts, and records the order its passes and the
/// window's are begun and submitted in.
/// </summary>
public class RendererTargetPoolTests
{
    private sealed class CountingDevice : IRhiDevice
    {
        public int Created { get; private set; }
        public int Live { get; private set; }
        public List<string> Log { get; } = new();

        public IRhiRenderTarget CreateRenderTarget(int width, int height)
        {
            Created++;
            Live++;
            return new Target(this, width, height);
        }

        public IRhiTexture CreateTexture(TextureData data) => new Texture(data.Width, data.Height);

        public IRhiCommandList Begin(IRhiRenderTarget target, Color clearColor)
        {
            var pass = (Target)target;
            pass.Released.Should().BeFalse("a pass never draws into a released target");
            Log.Add($"begin {pass.Name}");
            return new Pass(this, pass.Name);
        }

        public void Dispose() { }

        internal sealed class Target(CountingDevice device, int width, int height) : IRhiRenderTarget
        {
            private static int _next;
            public string Name { get; } = $"target{++_next}";
            public bool Released { get; private set; }
            public int Width => width;
            public int Height => height;
            public void ReadPixelsSrgb(Span<byte> destinationRgba) { }

            public void Dispose()
            {
                Released.Should().BeFalse("a target is released once");
                Released = true;
                device.Live--;
            }
        }
    }

    private sealed class Texture(int width, int height) : IRhiTexture
    {
        public int Width => width;
        public int Height => height;
        public void Dispose() { }
    }

    private sealed class Pass(CountingDevice device, string name) : IRhiCommandList
    {
        public void SetPipeline(RhiPipelineKind kind) { }

        public void BindTexture(int slot, IRhiTexture texture, RhiFilter filter = RhiFilter.Nearest)
        {
            if (texture is CountingDevice.Target target)
                target.Released.Should().BeFalse("a pass never reads a released target");
        }

        public void Draw(in DrawUniforms uniforms) { }
        public void Submit(bool waitUntilCompleted) => device.Log.Add($"submit {name}");
    }

    /// <summary>A window's frame: its own pass, logged where the device logs its passes.</summary>
    private sealed class Window(CountingDevice device, int width = 64, int height = 64) : IRhiFrameTarget
    {
        public int Width => width;
        public int Height => height;

        public IRhiCommandList Begin(Color clearColor)
        {
            device.Log.Add("begin window");
            return new Pass(device, "window");
        }

        public void Submit(IRhiCommandList commands) => commands.Submit(waitUntilCompleted: false);
    }

    private static DisplayList Layers(int nested = 1, int siblings = 1)
    {
        var builder = new DisplayListBuilder();
        builder.Clear(Color.White);
        for (var s = 0; s < siblings; s++)
        {
            for (var n = 0; n < nested; n++) builder.PushLayer(0.5f);
            builder.FillRect(new Rect(0, 0, 10, 10), Paint.Solid(Color.Black));
            for (var n = 0; n < nested; n++) builder.PopLayer();
        }
        return builder.Build();
    }

    private static DisplayList ABackdrop()
    {
        var builder = new DisplayListBuilder();
        builder.Clear(Color.White);
        builder.FillRect(new Rect(0, 0, 10, 10), Paint.Solid(Color.Black));
        builder.BackdropBlur(new RRect(new Rect(0, 0, 32, 32), new CornerRadii(0)), 8);
        builder.FillRect(new Rect(4, 4, 10, 10), Paint.Solid(Color.Black));
        return builder.Build();
    }

    private static DisplayList Nothing()
    {
        var builder = new DisplayListBuilder();
        builder.Clear(Color.White);
        return builder.Build();
    }

    [Fact]
    public void ALayerDrawnEveryFrame_IsOneTarget()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);

        for (var frame = 0; frame < 100; frame++) renderer.Render(Layers(), new Window(device));

        device.Created.Should().Be(1, "the layer's target is the pool's, kept from one frame to the next");
    }

    [Theory]
    [InlineData(2, 1, 2)]   // a layer inside a layer: both are held while the outer one draws
    [InlineData(1, 3, 3)]   // three layers side by side: each is held until the window composites it
    public void TheLayersOfAFrame_AreAsManyTargets_EveryFrame(int nested, int siblings, int targets)
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);

        for (var frame = 0; frame < 50; frame++) renderer.Render(Layers(nested, siblings), new Window(device));

        device.Created.Should().Be(targets);
    }

    [Fact]
    public void ABackdropDrawnEveryFrame_TakesItsTargetsFromThePool()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);
        renderer.Render(ABackdrop(), new Window(device));
        var first = device.Created;

        for (var frame = 0; frame < 50; frame++) renderer.Render(ABackdrop(), new Window(device));

        first.Should().BeGreaterThan(2, "the content below the glass, its blur, and the blur's levels");
        device.Created.Should().Be(first, "every later frame draws into the targets the first one made");
    }

    /// <summary>
    /// The order the window path's last pass meets its layers in: every layer is submitted before the
    /// window's pass begins, so on a device that runs its submissions apart the window still reads a
    /// layer that is finished.
    /// </summary>
    [Fact]
    public void EveryLayer_IsSubmittedBeforeTheWindowsPassBegins()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);

        renderer.Render(Layers(nested: 2, siblings: 2), new Window(device));

        var window = device.Log.IndexOf("begin window");
        window.Should().BeGreaterThan(0);
        device.Log.Take(window).Count(entry => entry.StartsWith("submit ")).Should().Be(4,
            "four layers, each submitted before the window's pass opens");
        device.Log.Skip(window).Should().Equal("begin window", "submit window");
    }

    [Theory]
    [InlineData(RhiRenderer.RetainFrames, 1)]        // away for exactly as long as it is kept: still there
    [InlineData(RhiRenderer.RetainFrames + 1, 0)]    // one frame longer: released
    public void ATargetNoFrameRents_IsReleasedAsARasterIs(int framesAway, int live)
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);
        renderer.Render(Layers(), new Window(device));

        for (var frame = 0; frame < framesAway; frame++) renderer.Render(Nothing(), new Window(device));
        // A target is counted away at the start of the frame after one that left it out.
        renderer.Render(Nothing(), new Window(device));

        device.Live.Should().Be(live);
    }

    [Fact]
    public void AResize_ReleasesTheSizeItLeft()
    {
        var device = new CountingDevice();
        using var renderer = new RhiRenderer(device);

        // A window being resized: every frame a new size, each with its layer.
        for (var width = 64; width < 94; width++) renderer.Render(Layers(), new Window(device, width, 64));

        device.Live.Should().Be(1, "a resize holds one frame's targets, not thirty frames of sizes it has left");
    }

    [Fact]
    public void DisposingTheRenderer_ReleasesThePool()
    {
        var device = new CountingDevice();
        var renderer = new RhiRenderer(device);
        renderer.Render(Layers(nested: 2), new Window(device));
        renderer.Render(ABackdrop(), new Window(device));

        renderer.Dispose();

        device.Live.Should().Be(0);
    }

    [Fact]
    public void APopWithNoLayerOpen_IsRefused()
    {
        var builder = new DisplayListBuilder();

        var pop = () => builder.PopLayer();

        pop.Should().Throw<InvalidOperationException>(
            "a scope that ends before it begins has no target to composite from");
    }
}
