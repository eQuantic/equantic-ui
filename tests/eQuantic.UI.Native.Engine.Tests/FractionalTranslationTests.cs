using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A translation in fractions of the box's own size (#614), Flutter's <c>FractionalTranslation</c>:
/// the handoff's sheen band starts 130% of its width outside the button it crosses, whatever that
/// width is. The web half is <c>FractionalTranslationRealizerTests</c>.
/// </summary>
public class FractionalTranslationTests
{
    private static readonly ColorToken Fill = new(Color.FromRgb(0x11, 0x22, 0x33));

    private static DrawCommand FillOf(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 400, 100, PhotonTheme.Instance, ThemeMode.Light, ContrastLevel.Standard, builder);
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect) return command;
        throw new InvalidOperationException("the box painted no fill");
    }

    [Fact]
    public void AFraction_IsOfTheBoxsOwnWidth()
    {
        var moved = FillOf(new Box(new BoxStyle
        {
            Width = 200, Height = 40, Background = Fill,
            Transform = Transform2D.FractionalTranslate(-1.3f),
        }));

        moved.Transform.M31.Should().BeApproximately(-260, 0.01f, "130% of a 200-unit box");
        moved.Transform.M32.Should().Be(0);
    }

    [Fact]
    public void AFractionAndUnits_AddUp()
    {
        var moved = FillOf(new Box(new BoxStyle
        {
            Width = 100, Height = 40, Background = Fill,
            Transform = Transform2D.Translate(2, 3).WithFractionalTranslate(0.5f, -1),
        }));

        moved.Transform.M31.Should().BeApproximately(52, 0.01f, "half of 100, and 2 more");
        moved.Transform.M32.Should().BeApproximately(-37, 0.01f, "minus the whole 40, and 3 more");
    }

    [Fact]
    public void Resolved_TurnsFractionsIntoUnits_AndLeavesTheRest()
    {
        var resolved = Transform2D.FractionalTranslate(-1.3f, 0.5f).WithRotate(10).Resolved(200, 40);

        resolved.Should().Be(new Transform2D(TranslateX: -260, TranslateY: 20, RotationDegrees: 10));
        Transform2D.FractionalTranslate(1).IsIdentity.Should().BeFalse("a fraction alone still moves the box");
    }
}
