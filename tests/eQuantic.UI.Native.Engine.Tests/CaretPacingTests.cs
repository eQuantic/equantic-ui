using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The caret is 2Hz motion, not vsync motion. Holding NeedsRender for it pinned the render loop to
/// the display's whole refresh — 120.3 presents a second, measured on a ProMotion panel, to flip a
/// rectangle twice a second. An editing caret now SCHEDULES: NeedsRender stays false, the next
/// frame is due at the next blink transition, and a shell's idle tick asks <c>IsFrameDue</c>.
/// <para>
/// The shells cannot be unit-tested (they need a window), so the harness below drives the same
/// contract they consume: tick every 8ms like a 120Hz timer, present only when dirty or due, and
/// COUNT. The count is the whole point.
/// </para>
/// </summary>
public class CaretPacingTests
{
    private sealed class Page : Primitives.StatefulComponent
    {
        public string Value = "steady";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new TextEntry(Value, v => SetState(() => Value = v)));
            return column;
        }
    }

    private static (PhotonHost Host, Rect Field) Open()
    {
        var host = new PhotonHost(new Page(), PhotonTheme.Instance, ThemeMode.Light, 400, 200);
        var frame = host.RenderFrame(new DisplayListBuilder());
        return (host, frame.TextRegions.Single().Bounds);
    }

    private static void ClickInto(PhotonHost host, Rect field)
    {
        host.PressDown(field.Center.X, field.Center.Y);
        host.PressUp(field.Center.X, field.Center.Y);
    }

    // ---- the contract ----------------------------------------------------------------------------

    [Fact]
    public void AnEditingCaretNoLongerPinsTheLoop()
    {
        var (host, field) = Open();
        ClickInto(host, field);
        host.RenderFrame(new DisplayListBuilder(), 100);

        host.NeedsRender.Should().BeFalse(
            "a caret is not vsync motion — holding NeedsRender for it presented 120 frames a "
            + "second to flip a rectangle at 2Hz");
    }

    [Fact]
    public void ButTheNextBlinkIsSCHEDULED()
    {
        var (host, field) = Open();
        ClickInto(host, field);
        host.RenderFrame(new DisplayListBuilder(), 100);   // phase started at the click

        host.IsFrameDue(101).Should().BeFalse("the blink has half a second to go");
        host.IsFrameDue(400).Should().BeFalse();
        host.IsFrameDue(700).Should().BeTrue("the half-period elapsed — the caret must turn off");
    }

    [Fact]
    public void RenderingTheDueFrameFlipsTheBlink_AndSchedulesTheNext()
    {
        var (host, field) = Open();
        ClickInto(host, field);
        host.RenderFrame(new DisplayListBuilder(), 0);

        host.CaretVisible.Should().BeTrue("the first half of the cycle is ON");

        // The shell's tick finds the frame due and presents it.
        host.IsFrameDue(520).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 520);
        host.CaretVisible.Should().BeFalse("the flip is what the frame was for");

        host.IsFrameDue(521).Should().BeFalse("…and the NEXT transition is scheduled");
        host.IsFrameDue(1010).Should().BeTrue();
    }

    [Fact]
    public void WithNoCaretNothingIsScheduled()
    {
        var (host, _) = Open();
        host.RenderFrame(new DisplayListBuilder(), 0);

        host.NeedsRender.Should().BeFalse();
        host.IsFrameDue(10_000).Should().BeFalse("a still page has no frame due, ever");
    }

    [Fact]
    public void ADragDrawsThroughThePointer_NotThroughASchedule()
    {
        var (host, field) = Open();
        host.PressDown(field.X + 1, field.Center.Y);
        host.PointerMove(field.X + 30, field.Center.Y);
        host.RenderFrame(new DisplayListBuilder(), 100);

        // Mid-drag the caret is forced solid (no blink), so there is no transition to schedule —
        // the pointer's own movement is what asks for frames.
        host.IsFrameDue(10_000).Should().BeFalse();
        host.CaretVisible.Should().BeTrue();
        host.PressUp(field.X + 30, field.Center.Y);
    }

    /// <summary>Real motion still pins the loop — the schedule is only for the caret.</summary>
    [Fact]
    public void RunningMotionStillRendersEveryFrame()
    {
        var spinner = new Column(gap: 0) { Width = SizeValue.Fill };
        spinner.Add(new Spinner(IconSize.Md, PhotonTheme.Instance.Colors(Variant.Primary).Base));
        var host = new PhotonHost(spinner, PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        host.RenderFrame(new DisplayListBuilder(), 0);

        host.NeedsRender.Should().BeTrue("a spinner is vsync motion and keeps the loop hot");
    }

    // ---- the COUNT — the measurement that started this -------------------------------------------

    /// <summary>
    /// Ten simulated seconds of a shell's 120Hz idle tick over a focused field: the old contract
    /// presented ~1200 frames; the paced one presents the blink's own 20 (one per half-period),
    /// and every one lands exactly on a transition.
    /// </summary>
    [Fact]
    public void TenSecondsOfFocusedField_PresentsTwentyFrames_NotTwelveHundred()
    {
        var (host, field) = Open();
        ClickInto(host, field);
        host.RenderFrame(new DisplayListBuilder(), 0);   // the click's own frame, phase 0

        var presents = 0;
        var visible = new List<bool>();
        for (var tick = 1; tick <= 1200; tick++)          // 120Hz for 10 simulated seconds
        {
            var nowMs = tick * (10_000f / 1200f);
            if (!host.NeedsRender && !host.IsFrameDue(nowMs)) continue;
            host.RenderFrame(new DisplayListBuilder(), nowMs);
            presents++;
            visible.Add(host.CaretVisible);
        }

        presents.Should().Be(20, "one present per blink transition over ten seconds — 2Hz, "
            + "not the display's refresh rate");
        // …and every presented frame flipped the caret, so the blink LOOKS identical.
        for (var i = 1; i < visible.Count; i++)
            visible[i].Should().Be(!visible[i - 1], $"present {i} must alternate the blink");
    }
}
