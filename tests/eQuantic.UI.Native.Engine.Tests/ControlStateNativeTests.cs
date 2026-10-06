using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A control's press and focus on Photon (#508): the <see cref="Pressable"/> is what is pressed and
/// what takes focus, and every box inside it shows its <c>Pressed</c> and <c>Focus</c> diffs while it
/// is, in the handoff's order, pressed over focus over hover. The web twin is the
/// <c>.eq-pressable:active</c> and <c>.eq-pressable:focus-visible</c> rule families.
/// </summary>
public class ControlStateNativeTests
{
    private static readonly ColorToken Fill = new(Color.FromRgb(0x11, 0x22, 0x33));

    private static Box Surface(StyleDiff? hover = null, StyleDiff? focus = null, StyleDiff? pressed = null) =>
        new(new BoxStyle
        {
            Width = 40, Height = 40, Background = Fill,
            Hover = hover, Focus = focus, Pressed = pressed,
        });

    private static PhotonHost Host(VisualNode root, float width = 40) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, 40);

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }

    private static DrawCommand[] Fills(DisplayList list) =>
        list.Commands.ToArray().Where(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Fill.Light).ToArray();

    /// <summary>The alpha of the one group-opacity layer the frame opens, or 1 when it opens none.</summary>
    private static float LayerAlpha(DisplayList list) =>
        list.Commands.ToArray().Where(c => c.Kind == DrawCommandKind.BeginLayer).Select(c => c.StrokeWidth)
            .DefaultIfEmpty(1f).Single();

    [Fact]
    public void APressedControl_ScalesTheBoxInsideIt()
    {
        var host = Host(new Pressable(Surface(pressed: new StyleDiff { Transform = Transform2D.Scale(0.5f) }), () => { }));

        Fills(Frame(host)).Single().Transform.M11.Should().Be(1, "nothing is pressed yet");
        host.PressDown(20, 20);
        Fills(Frame(host)).Single().Transform.M11.Should().BeApproximately(0.5f, 1e-5f,
            "the press is the control's, and its box shows it");
    }

    [Fact]
    public void AFocusedControl_DrawsTheFocusDiffOfTheBoxInsideIt()
    {
        var host = Host(new Pressable(Surface(focus: new StyleDiff { Opacity = 0.5f }), () => { }));

        LayerAlpha(Frame(host)).Should().Be(1);
        host.FocusNext().Should().BeTrue();
        LayerAlpha(Frame(host)).Should().Be(0.5f, "the control has keyboard focus, and its box shows it");
    }

    /// <summary>The handoff's §10: pressed beats hover, and the focus sits between them, as the web's
    /// selectors order them by specificity.</summary>
    [Fact]
    public void PressedBeatsFocus_AndFocusBeatsHover()
    {
        static StyleDiff Fade(float alpha) => new() { Opacity = alpha };
        Pressable Control() => new(Surface(hover: Fade(0.8f), focus: Fade(0.6f), pressed: Fade(0.4f)), () => { });

        var host = Host(Control());
        Frame(host);
        host.PointerMove(20, 20);
        LayerAlpha(Frame(host)).Should().Be(0.8f, "hovered");
        host.FocusNext().Should().BeTrue();
        LayerAlpha(Frame(host)).Should().Be(0.6f, "focus beats hover");

        // A pointer press keeps the focus and hides it, as :focus-visible does on the web, so the
        // three states are never held together by input on either target: the order between press
        // and focus is asked of a picture.
        host.PressDown(20, 20);
        host.Focused.Should().NotBeNull("the control keeps its focus");
        host.FocusVisible.Should().BeFalse("a pointer press hides it, as the web's :focus-visible does");
        LayerAlpha(Frame(host)).Should().Be(0.4f, "pressed beats hover");
        LayerAlpha(Frame(Host(new Simulated(
                SimulatedState.Hovered | SimulatedState.Focused | SimulatedState.Pressed, Control()))))
            .Should().Be(0.4f, "pressed beats focus");
    }

    /// <summary>The control's state is its SUBTREE's: a box beside the pressed control, with a press of
    /// its own declared, stays as it is.</summary>
    [Fact]
    public void TheControlsStateEndsWithItsSubtree()
    {
        var row = new Row(gap: 0)
        {
            new Pressable(Surface(pressed: new StyleDiff { Transform = Transform2D.Scale(0.5f) }), () => { }),
            Surface(pressed: new StyleDiff { Transform = Transform2D.Scale(0.5f) }),
        };
        var host = Host(row, width: 80);
        Frame(host);

        host.PressDown(20, 20);
        var fills = Fills(Frame(host));
        fills.Should().HaveCount(2);
        fills[0].Transform.M11.Should().BeApproximately(0.5f, 1e-5f, "the pressed control's box");
        fills[1].Transform.M11.Should().Be(1, "a box outside it");
    }

    [Fact]
    public void ASimulatedPressAndFocus_DrawTheirDiffs()
    {
        var pressed = Host(new Simulated(SimulatedState.Pressed,
            new Pressable(Surface(pressed: new StyleDiff { Transform = Transform2D.Scale(0.5f) }), () => { })));
        Fills(Frame(pressed)).Single().Transform.M11.Should().BeApproximately(0.5f, 1e-5f);

        var focused = Host(new Simulated(SimulatedState.Focused,
            new Pressable(Surface(focus: new StyleDiff { Opacity = 0.5f }), () => { })));
        LayerAlpha(Frame(focused)).Should().Be(0.5f);
    }

    /// <summary>A control that becomes disabled while it has focus, the button that turns to "loading"
    /// under the keyboard. The focus walk skips a disabled control but the focus it already holds is
    /// kept by path, and the handoff's "disabled mutes everything" holds there too, as the web's
    /// families never select under a disabled control (it carries no <c>eq-pressable</c>).</summary>
    private sealed class Loading : StatefulComponent
    {
        public bool Busy;
        public void Start() => SetState(() => Busy = true);

        public override VisualNode Build(ComponentContext context) =>
            new Pressable(Surface(focus: new StyleDiff { Opacity = 0.5f }), () => { }) { Disabled = Busy };
    }

    [Fact]
    public void AControlDisabledWhileFocused_StopsShowingTheFocus()
    {
        var control = new Loading();
        var host = Host(control);
        Frame(host);
        host.FocusNext().Should().BeTrue();
        LayerAlpha(Frame(host)).Should().Be(0.5f, "focused");

        control.Start();
        LayerAlpha(Frame(host)).Should().Be(1, "a disabled control mutes its states, focused or not");
    }

    private static readonly ColorToken PressedFill = new(Color.FromRgb(0xAA, 0x00, 0x00));

    /// <summary>A control that goes disabled while it is held, the press and the focus both. The host
    /// tracks them by path, so the rebuilt, disabled control is still the tracked one, and it must
    /// arm neither its pressed fill nor the ring (found by Copilot on #617).</summary>
    private sealed class Guarded : StatefulComponent
    {
        public bool Off;
        public void Disable() => SetState(() => Off = true);

        public override VisualNode Build(ComponentContext context) =>
            new Pressable(Surface(), () => { }) { Disabled = Off, PressedBackground = PressedFill };
    }

    private static bool Paints(DisplayList list, ColorToken token) =>
        list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == token.Light);

    private static bool Rings(DisplayList list) =>
        list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.StrokeRRect
            && c.Paint.Color == PhotonTheme.Instance.FocusRing.Resolve(ThemeMode.Light));

    [Fact]
    public void AControlDisabledWhilePressed_DrawsNoPressedFill()
    {
        var control = new Guarded();
        var host = Host(control);
        Frame(host);
        host.PressDown(20, 20);
        Paints(Frame(host), PressedFill).Should().BeTrue("pressed");

        control.Disable();
        Paints(Frame(host), PressedFill).Should().BeFalse("a disabled control shows no press");
    }

    [Fact]
    public void AControlDisabledWhileFocused_DrawsNoRing()
    {
        var control = new Guarded();
        var host = Host(control);
        Frame(host);
        host.FocusNext().Should().BeTrue();
        Rings(Frame(host)).Should().BeTrue("focused from the keyboard");

        control.Disable();
        Rings(Frame(host)).Should().BeFalse("a disabled control shows no focus");
    }

    [Fact]
    public void ASimulatedPressOfADisabledControl_DrawsNothing()
    {
        var host = Host(new Simulated(SimulatedState.Pressed | SimulatedState.Focused,
            new Pressable(Surface(), () => { }) { Disabled = true, PressedBackground = PressedFill }));
        var frame = Frame(host);

        Paints(frame, PressedFill).Should().BeFalse("the web gives a disabled control no eq-pressed");
        Rings(frame).Should().BeFalse("nor eq-focused");
    }

    /// <summary>The state lends its subtree nothing when the thing focused is not a control: a Link
    /// takes focus and draws its own ring, and the web's focus family selects only under
    /// <c>eq-pressable</c>.</summary>
    [Fact]
    public void ABoxOutsideAnyControl_NeverShowsAFocusOrAPress()
    {
        var host = Host(Surface(focus: new StyleDiff { Opacity = 0.5f }, pressed: new StyleDiff { Opacity = 0.4f }));
        Frame(host);

        host.PressDown(20, 20);
        LayerAlpha(Frame(host)).Should().Be(1, "a press needs a control to press");
    }
}
