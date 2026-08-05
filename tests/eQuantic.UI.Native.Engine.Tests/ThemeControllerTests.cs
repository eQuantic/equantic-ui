using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The light/dark hand (<see cref="IThemeController"/>): a component calls Apply, the HOST's next
/// frame paints the same tree against the other palette. The controller exists before the window
/// does, so the sink attaches late — an Apply before the attach records the mode and nothing else.
/// </summary>
public class ThemeControllerTests
{
    [Fact]
    public void Apply_FlipsTheHostsPalette_OnTheNextFrame()
    {
        var host = new PhotonHost(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = 10 }),
            PhotonTheme.Instance, ThemeMode.Light, 100, 50);
        var controller = new PhotonThemeController();
        controller.Attach(ThemeMode.Light, mode =>
        {
            host.Mode = mode;
            host.Invalidate();
        });

        var lightBuilder = new DisplayListBuilder();
        host.RenderFrame(lightBuilder);
        var lightFrame = lightBuilder.Build();
        lightFrame.Commands[0].Kind.Should().Be(DrawCommandKind.Clear);
        var light = lightFrame.Commands[0].Paint;

        controller.Apply(ThemeMode.Dark);
        host.NeedsRender.Should().BeTrue("Apply invalidates — the flip must not wait for other input");

        var darkBuilder = new DisplayListBuilder();
        host.RenderFrame(darkBuilder);
        var dark = darkBuilder.Build().Commands[0].Paint;

        controller.Mode.Should().Be(ThemeMode.Dark);
        dark.Should().NotBe(light, "the background token resolves per mode, and the clear is the background");
    }

    [Fact]
    public void ApplyBeforeAttach_JustRecordsTheMode()
    {
        var controller = new PhotonThemeController();
        controller.Apply(ThemeMode.Dark);
        controller.Mode.Should().Be(ThemeMode.Dark, "a component may flip before any window exists");

        var seen = ThemeMode.Light;
        controller.Attach(ThemeMode.Light, mode => seen = mode);
        controller.Mode.Should().Be(ThemeMode.Light, "the attach seeds from what the window opened with");

        controller.Apply(ThemeMode.Dark);
        seen.Should().Be(ThemeMode.Dark);
    }
}
