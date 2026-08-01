using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S3 on Photon — BoxStyle.BackdropBlur becomes ONE BackdropBlur command, recorded before the
/// box's own chrome (the glass sits under the translucent fill), with the rrect baked to device
/// space in the Clip slot and the radius (device px) riding StrokeWidth. The web twin pins the CSS
/// declarations in S3BackdropBlurRealizerTests.
/// </summary>
public class S3BackdropBlurNativeTests
{
    private static DisplayList Realize(VisualNode root)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(root, 400, 300, PhotonTheme.Instance, ThemeMode.Light, builder);
        return builder.Build();
    }

    [Fact]
    public void BackdropBlur_RecordsTheCommand_BeforeTheFill()
    {
        var glass = new Box(new BoxStyle
        {
            Width = 200, Height = 60, CornerRadius = new CornerRadii(30),
            BackdropBlur = 12,
            Background = new ColorToken(new Color(0xFF, 0xFF, 0xFF, 40)),
        });

        var commands = Realize(glass).Commands.ToArray();
        var blurIndex = Array.FindIndex(commands, c => c.Kind == DrawCommandKind.BackdropBlur);
        var fillIndex = Array.FindLastIndex(commands, c => c.Kind == DrawCommandKind.FillRRect);

        blurIndex.Should().BeGreaterThan(-1, "the style must lower to the engine command");
        blurIndex.Should().BeLessThan(fillIndex, "the glass blurs the backdrop UNDER its own fill");

        var blur = commands[blurIndex];
        blur.StrokeWidth.Should().Be(12f);
        blur.Clip.Should().NotBeNull("the region rrect is baked to device space in the Clip slot");
        blur.Clip!.Value.Rect.Width.Should().Be(200);
        blur.Clip.Value.Radii.TopLeft.Should().Be(30);
    }

    [Fact]
    public void ZeroBlur_RecordsNothing()
    {
        var plain = new Box(new BoxStyle { Width = 200, Height = 60, Background = new ColorToken(Color.FromRgb(20, 20, 30)) });
        Realize(plain).Commands.ToArray()
            .Should().NotContain(c => c.Kind == DrawCommandKind.BackdropBlur);
    }
}
