using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's half of contrast levels (#340): the shell reads the system's setting into
/// <see cref="PhotonHost.Contrast"/>, and every token the frame paints resolves with it beside the
/// mode. The same tree, the same mode, two levels: the fill a theme steps up changes, and one it
/// leaves where it is does not.
/// </summary>
public class TheHostPaintsTheContrastLevelTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static VisualNode Fills() => new Column
    {
        new Box(new BoxStyle { Width = 60, Height = 30, Background = Theme.Colors(Variant.Primary).Base }),
        new Box(new BoxStyle { Width = 60, Height = 30, Background = Theme.SurfaceSubtle }),
    };

    private static Color[] PaintedFills(ContrastLevel contrast)
    {
        var host = new PhotonHost(Fills(), Theme, ThemeMode.Dark, 300, 200) { Contrast = contrast };
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build().Commands.ToArray().Where(command => command.Kind == DrawCommandKind.FillRRect)
            .Select(command => command.Paint.Color).ToArray();
    }

    [Fact]
    public void AtMore_AFillTheThemeStepsUp_PaintsItsMoreLeg()
    {
        var primary = Theme.Colors(Variant.Primary).Base;

        PaintedFills(ContrastLevel.Standard).Should().Contain(primary.Dark).And.NotContain(primary.MoreDark);
        PaintedFills(ContrastLevel.More).Should().Contain(primary.MoreDark).And.NotContain(primary.Dark);
        primary.MoreDark.Should().Be(Color.FromRgb(0xAE, 0xD5, 0xFE), "the handoff's More fill for Primary in dark mode");
    }

    [Fact]
    public void AtMore_AFillTheThemeLeavesAlone_PaintsTheSame()
    {
        Theme.SurfaceSubtle.DiffersAtMore.Should().BeFalse();
        PaintedFills(ContrastLevel.More).Should().Contain(Theme.SurfaceSubtle.Dark);
    }

    [Fact]
    public void AChangeOfLevel_RepaintsTheSameHost()
    {
        var host = new PhotonHost(Fills(), Theme, ThemeMode.Dark, 300, 200);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        host.Contrast = ContrastLevel.More;
        host.Invalidate();
        builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        builder.Build().Commands.ToArray().Where(command => command.Kind == DrawCommandKind.FillRRect)
            .Select(command => command.Paint.Color)
            .Should().Contain(Theme.Colors(Variant.Primary).Base.MoreDark, "the shell sets the level and asks for a frame");
    }
}
