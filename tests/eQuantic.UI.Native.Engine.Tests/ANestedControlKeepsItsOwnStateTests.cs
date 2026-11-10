using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A control's press and focus reach the boxes of THAT control, and stop at a control nested in it
/// (#616): a button in a pressed row is not pressed, and a picture of the row pictures the row.
/// <para>
/// Until #616 a pressed fill reached only the first box drawn under the pressed control, so a fill
/// never crossed into a nested control, while the rest of a <c>Pressed</c> diff did. Once the fill
/// became a member of the diff, pressing a row filled every button inside it. The web half is
/// <c>ANestedControlKeepsItsOwnStateRealizerTests</c> and the shared parity fixture's
/// <c>nested-control-simulated</c> case.
/// </para>
/// </summary>
public class ANestedControlKeepsItsOwnStateTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static ColorToken Solid(byte r) => new(Color.FromRgb(r, 0x40, 0x60));

    private static readonly ColorToken OuterPressed = Solid(0x11), OuterFocused = Solid(0x12);
    private static readonly ColorToken InnerPressed = Solid(0x21), InnerFocused = Solid(0x22);

    /// <summary>A 300x60 row whose right end holds a 40x40 control of its own.</summary>
    private static VisualNode Row() => new Pressable(new Box(new BoxStyle
    {
        Width = 300,
        Height = 60,
        Background = Theme.Surface,
        Pressed = new StyleDiff { Background = OuterPressed },
        Focus = new StyleDiff { Background = OuterFocused },
    }, Content()), () => { }) { Label = "Row" };

    private static Row Content()
    {
        var content = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.End,
            Cross = CrossAlign.Center,
        };
        content.Add(new Pressable(new Box(new BoxStyle
        {
            Width = 40,
            Height = 40,
            Background = Theme.SurfaceSubtle,
            Pressed = new StyleDiff { Background = InnerPressed },
            Focus = new StyleDiff { Background = InnerFocused },
        }), () => { }) { Label = "Inner" });
        return content;
    }

    private static bool Paints(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light);
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color)
                return true;
        return false;
    }

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    private static DisplayList Draw(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 400, 200, Theme, ThemeMode.Light, builder);
        return builder.Build();
    }

    [Fact]
    public void PressingTheRow_PressesTheRow_AndNotTheControlInIt()
    {
        var host = new PhotonHost(Row(), Theme, ThemeMode.Light, 400, 200);
        Frame(host);
        host.PressDown(20, 30);
        var frame = Frame(host);

        Paints(frame, OuterPressed).Should().BeTrue("the row is pressed");
        Paints(frame, InnerPressed).Should().BeFalse("the control in the row was not pressed");
    }

    [Fact]
    public void PressingTheControl_PressesTheControl_AndNotTheRow()
    {
        var host = new PhotonHost(Row(), Theme, ThemeMode.Light, 400, 200);
        Frame(host);
        host.PressDown(280, 30);

        var frame = Frame(host);
        Paints(frame, InnerPressed).Should().BeTrue("the control the pointer went down on is pressed");
        Paints(frame, OuterPressed).Should().BeFalse("and the row around it is not: one control is pressed");
    }

    [Fact]
    public void FocusingTheRow_FocusesTheRow_AndNotTheControlInIt()
    {
        var host = new PhotonHost(Row(), Theme, ThemeMode.Light, 400, 200);
        Frame(host);
        host.FocusNext().Should().BeTrue();
        var row = Frame(host);
        Paints(row, OuterFocused).Should().BeTrue("the row takes focus");
        Paints(row, InnerFocused).Should().BeFalse("the control in the row is not the focused one");
        // The control in the row is never a stop of its own here: a Pressable is the one stop for
        // its subtree (EmitPressable), so the row is all a keyboard reaches.
    }

    [Fact]
    public void APictureOfTheRow_PicturesTheRow_AndNotTheControlInIt()
    {
        var pressed = Draw(new Simulated(SimulatedState.Pressed, Row()));
        Paints(pressed, OuterPressed).Should().BeTrue();
        Paints(pressed, InnerPressed).Should().BeFalse("a picture stops where a real press does");

        var focused = Draw(new Simulated(SimulatedState.Focused, Row()));
        Paints(focused, OuterFocused).Should().BeTrue();
        Paints(focused, InnerFocused).Should().BeFalse("a picture stops where a real focus does");

        // And the ring is the row's alone: as many ring strokes as one control pictured focused.
        var one = Draw(new Simulated(SimulatedState.Focused, new Pressable(new Box(new BoxStyle
        {
            Width = 300,
            Height = 60,
        }), () => { })));
        Rings(focused).Should().Be(Rings(one)).And.BePositive();
    }

    private static int Rings(DisplayList list)
    {
        var count = 0;
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.StrokeRRect) count++;
        return count;
    }

    [Fact]
    public void APictureOfTheControl_InsideTheRow_PicturesTheControl()
    {
        var pictured = new Pressable(new Box(new BoxStyle
        {
            Width = 300,
            Height = 60,
            Pressed = new StyleDiff { Background = OuterPressed },
        }, new Simulated(SimulatedState.Pressed, Content())), () => { });

        var frame = Draw(pictured);
        Paints(frame, InnerPressed).Should().BeTrue("a picture inside a control starts over");
        Paints(frame, OuterPressed).Should().BeFalse("and pictures nothing of the control around it");
    }
}
