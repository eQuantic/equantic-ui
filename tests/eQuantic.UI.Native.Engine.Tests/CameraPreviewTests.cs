using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The vocabulary's first LIVE node: a camera preview the engine composites as an ordinary
/// texture, one GPU slot mutated per frame instead of a new leaked texture each.
/// </summary>
public class CameraPreviewTests
{
    private sealed class FakeSession : ICameraSession
    {
        public string Id => "fake";
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public byte[]? FrameBytes { get; set; }
        public int FrameVersion { get; set; }
        public ImageData? Capture() => null;
        public void Dispose() { }
    }

    private static (RealizeResult Frame, DrawCommand[] Commands) Render(VisualNode root)
    {
        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(root, 400, 300, PhotonTheme.Instance, ThemeMode.Light, builder);
        return (frame, builder.Build().Commands.ToArray());
    }

    [Fact]
    public void WithoutASession_ItIsTheSamePlaceholderImageDegradesTo()
    {
        var (frame, commands) = Render(new CameraPreview(null, 320, 240));

        frame.HasActiveMotion.Should().BeFalse("nothing is running");
        var subtle = PhotonTheme.Instance.SurfaceSubtle.Resolve(ThemeMode.Light);
        commands.Should().Contain(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == subtle);
        commands.Should().NotContain(c => c.Kind == DrawCommandKind.Texture);
    }

    /// <summary>
    /// A camera answers its start before its first frame: the first render with a session has no
    /// pixels to draw. The host still has to keep asking, or nothing ever redraws the screen and the
    /// preview sits on its placeholder with the camera running behind it (#775, found on an Android
    /// emulator, where no pointer moves to redraw the screen by accident).
    /// </summary>
    [Fact]
    public void ASessionWithNoFrameYet_KeepsTheHostAsking()
    {
        var session = new FakeSession();   // started, nothing delivered

        var (frame, commands) = Render(new CameraPreview(session, 320, 240));

        frame.HasActiveMotion.Should().BeTrue("frames are about to arrive, and only a redraw shows them");
        commands.Should().NotContain(c => c.Kind == DrawCommandKind.Texture, "there is no pixel to draw yet");

        // The first frame lands in the session; the NEXT render, which the host now asks for, draws it.
        session.FrameWidth = 4;
        session.FrameHeight = 2;
        session.FrameBytes = new byte[4 * 2 * 4];
        session.FrameVersion = 1;
        var (_, next) = Render(new CameraPreview(session, 320, 240));
        next.Should().Contain(c => c.Kind == DrawCommandKind.Texture);
    }

    [Fact]
    public void WithFrames_OneTextureLivesAndItsVersionFollowsTheFeed()
    {
        var session = new FakeSession
        {
            FrameWidth = 4,
            FrameHeight = 2,
            FrameBytes = new byte[4 * 2 * 4],
            FrameVersion = 1,
        };
        var node = new CameraPreview(session, 320, 240);

        var (first, commands) = Render(node);
        first.HasActiveMotion.Should().BeTrue("frames keep arriving; the host must keep asking");
        commands.Should().Contain(c => c.Kind == DrawCommandKind.Texture);

        // The feed advances IN PLACE; the realizer mirrors the version onto the SAME TextureData,
        // which is what makes the renderer re-upload one GPU slot instead of minting a leak.
        session.FrameVersion = 2;
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 400, 300, PhotonTheme.Instance, ThemeMode.Light, builder);
        var textures = builder.Build().Textures;
        textures.Should().HaveCount(1);
        textures[0].Version.Should().Be(2);
        textures[0].Alpha.Should().BeSameAs(session.FrameBytes, "no copy sits between the feed and the GPU upload");
    }

    [Fact]
    public void TheBmpItCaptures_IsAValidBottomUpBgra()
    {
        // One red pixel over one blue: enough to pin the header, the swizzle and the row flip.
        var rgba = new byte[]
        {
            255, 0, 0, 255,   // top row: red
            0, 0, 255, 255,   // bottom row: blue
        };
        var file = Bmp.Encode(rgba, 1, 2);

        file[0].Should().Be((byte)'B');
        file[1].Should().Be((byte)'M');
        file.Length.Should().Be(54 + 8);
        // Bottom-up: the file's FIRST row is the image's LAST — blue, stored BGRA.
        file[54].Should().Be(255, "blue channel of the bottom pixel comes first");
        file[56].Should().Be(0, "and its red channel is empty");
        file[58].Should().Be(0, "the top pixel's blue channel is empty");
        file[60].Should().Be(255, "and its red channel is not");
    }
}
