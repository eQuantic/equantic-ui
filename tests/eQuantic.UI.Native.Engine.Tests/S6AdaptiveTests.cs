using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec S6 on Photon: an AdaptiveNode IS its resolved variant — the window size class
/// derives from the viewport width, and a resize across a threshold swaps the subtree.</summary>
public class S6AdaptiveTests
{
    private static Box Marker(byte r) => new(new BoxStyle
    {
        Width = 40, Height = 12, Background = new ColorToken(Color.FromRgb(r, 0, 0)),
    });

    [Theory]
    [InlineData(400f, 0x10)]   // compact
    [InlineData(700f, 0x20)]   // medium
    [InlineData(1000f, 0x30)]  // expanded
    public void AdaptiveNode_LaysOutTheVariantForTheWindowClass(float width, byte expected)
    {
        var adaptive = new AdaptiveNode(Marker(0x10), Marker(0x20), Marker(0x30));
        var host = new PhotonHost(adaptive, PhotonTheme.Instance, ThemeMode.Light, width, 200);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        var fills = new List<byte>();
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect)
                fills.Add(command.Paint.Color.R);

        fills.Should().Contain(expected).And.NotContain((byte)(expected == 0x10 ? 0x20 : 0x10),
            "only the matching variant measures and paints");
    }

    [Fact]
    public void MissingVariants_FallBackTowardCompact()
    {
        var adaptive = new AdaptiveNode(Marker(0x10), medium: null, expanded: Marker(0x30));
        WindowSizeClasses.FromWidth(700).Should().Be(WindowSizeClass.Medium);
        adaptive.Resolve(WindowSizeClass.Medium).Should().BeSameAs(adaptive.Compact,
            "no Medium variant → Compact serves the medium range");
        adaptive.Resolve(WindowSizeClass.Expanded).Should().BeSameAs(adaptive.Expanded);
    }
}
