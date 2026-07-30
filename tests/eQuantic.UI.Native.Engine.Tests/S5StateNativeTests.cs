using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec S5 on Photon: the hovered Box applies its Hover diff (fill swap here) — the same
/// declarative overlay the web realizes as a :hover rule, driven by the host's pointer tracking.</summary>
public class S5StateNativeTests
{
    [Fact]
    public void HoveredBox_AppliesItsHoverDiffFill()
    {
        var baseFill = new ColorToken(Color.FromRgb(0x11, 0x22, 0x33));
        var hoverFill = new ColorToken(Color.FromRgb(0xAA, 0xBB, 0xCC));
        var box = new Box(new BoxStyle
        {
            Width = 40, Height = 40,
            Background = baseFill,
            Hover = new StyleDiff { Background = hoverFill },
        });

        var host = new PhotonHost(box, PhotonTheme.Instance, ThemeMode.Light, 40, 40);

        var rest = new DisplayListBuilder();
        host.RenderFrame(rest);
        FillColors(rest.Build()).Should().Contain(baseFill.Resolve(ThemeMode.Light))
            .And.NotContain(hoverFill.Resolve(ThemeMode.Light));

        host.SetHovered(box);
        var hovered = new DisplayListBuilder();
        host.RenderFrame(hovered);
        FillColors(hovered.Build()).Should().Contain(hoverFill.Resolve(ThemeMode.Light),
            "the hovered Box swaps to its Hover diff fill");
    }

    private static List<Color> FillColors(DisplayList list)
    {
        var colors = new List<Color>();
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect)
                colors.Add(command.Paint.Color);
        return colors;
    }
}
