using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A hover inside a control is the control's (#614), as its focus and its press are: the handoff's
/// trailing arrow nudges 2 units when the BUTTON is hovered, the pointer on its label. A box outside
/// every control still hovers itself, and a control nested in a hovered one keeps its own. The web
/// half is <c>AHoverInsideAControlRealizerTests</c>.
/// </summary>
public class AHoverInsideAControlTests
{
    private static readonly ColorToken Label = new(Color.FromRgb(0x10, 0x20, 0x30));
    private static readonly ColorToken Arrow = new(Color.FromRgb(0x40, 0x50, 0x60));
    private static readonly ColorToken Inner = new(Color.FromRgb(0x70, 0x80, 0x90));

    /// <summary>A 300x40 control: a 260-unit label box, then a 40-unit arrow box that nudges on hover.</summary>
    private static VisualNode Button(VisualNode? nested = null)
    {
        var row = new Row(gap: 0) { Width = 300, Height = 40 };
        row.Add(new Box(new BoxStyle { Width = 260, Height = 40, Background = Label }));
        row.Add(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Arrow,
            Hover = new StyleDiff { Transform = Transform2D.Translate(2, 0) },
        }, nested));
        return new Pressable(row, () => { }) { Label = "Continue" };
    }

    private static DrawCommand FillIn(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light);
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color) return command;
        throw new InvalidOperationException("no fill of that colour");
    }

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    [Fact]
    public void TheArrowNudges_WithThePointerOnTheLabel()
    {
        var host = new PhotonHost(Button(), PhotonTheme.Instance, ThemeMode.Light, 400, 100);
        var rest = FillIn(Frame(host), Arrow).Transform.M31;

        host.PointerMove(100, 20);
        var hovered = FillIn(Frame(host), Arrow).Transform.M31;

        (hovered - rest).Should().Be(2, "the button is hovered, and the arrow is the button's");
    }

    [Fact]
    public void ABoxOutsideEveryControl_StillHoversItself()
    {
        var row = new Row(gap: 0) { Width = 300, Height = 40 };
        row.Add(new Box(new BoxStyle { Width = 260, Height = 40, Background = Label }));
        row.Add(new Box(new BoxStyle
        {
            Width = 40, Height = 40, Background = Arrow,
            Hover = new StyleDiff { Transform = Transform2D.Translate(2, 0) },
        }));
        var host = new PhotonHost(row, PhotonTheme.Instance, ThemeMode.Light, 400, 100);
        var rest = FillIn(Frame(host), Arrow).Transform.M31;

        host.PointerMove(100, 20);
        FillIn(Frame(host), Arrow).Transform.M31.Should().Be(rest, "nothing hovered the arrow itself");

        host.PointerMove(280, 20);
        (FillIn(Frame(host), Arrow).Transform.M31 - rest).Should().Be(2, "and now the pointer is on it");
    }

    [Fact]
    public void AControlNestedInAHoveredOne_KeepsItsOwnHover()
    {
        var nested = new Pressable(new Box(new BoxStyle
        {
            Width = 20, Height = 20, Background = Inner,
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, 3) },
        }), () => { });
        var host = new PhotonHost(Button(nested), PhotonTheme.Instance, ThemeMode.Light, 400, 100);
        var rest = FillIn(Frame(host), Inner).Transform.M32;

        host.PointerMove(100, 20);
        FillIn(Frame(host), Inner).Transform.M32.Should().Be(rest, "the outer control is hovered, not the nested one");
    }
}
