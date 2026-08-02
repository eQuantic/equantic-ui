using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The continuous gesture (spec S9). The host tracks the finger, the NODE carries the rules, and the
/// caller decides what a release meant — which is what lets one primitive serve a swipe-to-reveal, a
/// pull-to-refresh and a sheet's detents without any of them being special-cased.
/// </summary>
public class DraggableTests
{
    private static readonly ComponentContext Ctx = new(PhotonTheme.Instance);

    private sealed class Surface : Primitives.StatefulComponent
    {
        public float Released = float.NaN;
        public float Rest;
        public DragAxis Axis = DragAxis.Horizontal;

        public override VisualNode Build(ComponentContext context)
        {
            var child = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = 60,
                Background = context.Theme.Surface,
            });

            return new Draggable(child, offset => Released = offset)
            {
                Axis = Axis,
                Min = -96,
                Max = 96,
                RestOffset = Rest,
            };
        }
    }

    private static (PhotonHost Host, Surface Page, RealizeResult Frame) Settled(Surface? page = null)
    {
        page ??= new Surface();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 360, 200);
        host.RenderFrame(new DisplayListBuilder());
        return (host, page, host.RenderFrame(new DisplayListBuilder(), 1000));
    }

    [Fact]
    public void TheSurfaceRegistersForDragRouting()
    {
        var (_, _, frame) = Settled();
        frame.DragRegions.Should().ContainSingle();
        frame.DragRegions[0].Node.Should().BeOfType<Draggable>();
    }

    [Fact]
    public void ATapWithoutTravel_NeverArmsTheGesture()
    {
        var (host, page, frame) = Settled();
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PressUp(centre.X, centre.Y);

        float.IsNaN(page.Released).Should().BeTrue("a tap is a tap — the gesture never engaged");
    }

    [Fact]
    public void TravelOnTheOtherAxis_DoesNotArm()
    {
        // Without this a list holding a swipeable row cannot be scrolled: every vertical drag would
        // arm the row's horizontal gesture and swallow the scroll.
        var (host, page, frame) = Settled();
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X, centre.Y + 60);
        host.PressUp(centre.X, centre.Y + 60);

        float.IsNaN(page.Released).Should().BeTrue("the travel was across the gesture's axis");
    }

    [Fact]
    public void TravelOnItsAxis_FollowsTheFinger_AndReportsOnRelease()
    {
        var (host, page, frame) = Settled();
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X - 50, centre.Y);

        var mid = new DisplayListBuilder();
        host.RenderFrame(mid, 1100);
        mid.Build().Commands.ToArray()
            .Should().Contain(c => c.Transform == Matrix2D.Translation(-50, 0),
                "a horizontal gesture translates sideways, not down");

        host.PressUp(centre.X - 50, centre.Y);
        page.Released.Should().Be(-50f, "the caller is told where the finger left it");
    }

    [Fact]
    public void TravelPastTheLimits_Clamps()
    {
        var (host, page, frame) = Settled();
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X - 400, centre.Y);
        host.PressUp(centre.X - 400, centre.Y);

        page.Released.Should().Be(-96f, "Min is the caller's rule and the host honours it");
    }

    [Fact]
    public void AVerticalGesture_ArmsOnVerticalTravelOnly()
    {
        var page = new Surface { Axis = DragAxis.Vertical };
        var (host, _, frame) = Settled(page);
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X + 60, centre.Y);
        host.PressUp(centre.X + 60, centre.Y);
        float.IsNaN(page.Released).Should().BeTrue();

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X, centre.Y + 40);
        host.PressUp(centre.X, centre.Y + 40);
        page.Released.Should().Be(40f);
    }

    [Fact]
    public void RestOffset_PlacesTheSurfaceWithNoFingerOnIt()
    {
        // The node does not remember where it was left; the CALLER's state does. An already-open row
        // must render open on the very first frame, before anyone touches it.
        var page = new Surface { Rest = -96 };
        var (host, _, _) = Settled(page);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1100);
        builder.Build().Commands.ToArray()
            .Should().Contain(c => c.Transform == Matrix2D.Translation(-96, 0));
    }

    [Fact]
    public void ADragFromAnOpenRest_ReportsWhereItLANDS_NotHowFarItWent()
    {
        // A row already open at -96 dragged 40 back is at -56. Reporting the raw 40 would say the
        // row is OPEN in the wrong direction, and the web twin (which adds rest) would disagree.
        var page = new Surface { Rest = -96 };
        var (host, _, frame) = Settled(page);
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X + 40, centre.Y);
        host.PressUp(centre.X + 40, centre.Y);

        page.Released.Should().Be(-56f);
    }

    [Fact]
    public void ANormalizedGesture_ReportsAFractionOfItsOwnWidth()
    {
        // The component cannot know a fluid track's pixel width; the host can, so it converts.
        var page = new Fraction();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 360, 200);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);
        var region = frame.DragRegions[0];
        region.Bounds.Width.Should().Be(360, "the surface fills the host");

        host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
        host.PointerMove(region.Bounds.Center.X + 90, region.Bounds.Center.Y);

        page.Moved.Should().BeApproximately(0.5f + 90f / 360f, 1e-4f, "rest plus a quarter of the width");
    }

    [Fact]
    public void AGestureTheCallerPaints_DoesNotAlsoTranslate()
    {
        var page = new Fraction();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 360, 200);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);
        var region = frame.DragRegions[0];

        host.PressDown(region.Bounds.Center.X, region.Bounds.Center.Y);
        host.PointerMove(region.Bounds.Center.X + 90, region.Bounds.Center.Y);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1100);
        builder.Build().Commands.ToArray()
            .Should().NotContain(c => c.Transform == Matrix2D.Translation(90, 0),
                "the value already moved the subtree — a translate would move it twice");
    }

    [Fact]
    public void ACancelledGesture_DecidesNothing_AndGivesTheSurfaceBack()
    {
        var page = new Surface { Rest = -96 };
        var (host, _, frame) = Settled(page);
        var centre = frame.DragRegions[0].Bounds.Center;

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X + 40, centre.Y);
        host.PointerCancel();

        float.IsNaN(page.Released).Should().BeTrue("a cancel reports nothing — nothing was decided");

        // It glides back to the caller's rest rather than staying where the finger abandoned it.
        var landed = new DisplayListBuilder();
        host.RenderFrame(landed, 2000);
        landed.Build().Commands.ToArray()
            .Should().Contain(c => c.Transform == Matrix2D.Translation(-96, 0));
    }

    private sealed class Fraction : Primitives.StatefulComponent
    {
        public float Moved = float.NaN;

        public override VisualNode Build(ComponentContext context)
        {
            var child = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 60 });
            return new Draggable(child)
            {
                Axis = DragAxis.Horizontal,
                Normalized = true,
                Follows = false,
                Min = 0,
                Max = 1,
                RestOffset = 0.5f,
                OnMoved = offset => Moved = offset,
            };
        }
    }

    // ---- The two components that ride it ----------------------------------------------------

    [Fact]
    public void SwipeableRow_PutsItsActionBehindTheSurface()
    {
        var row = new SwipeableRow(new Text("Marcos"), "Dispute", Icons.Warning);
        var stack = ((Box)row.Build(Ctx)).Child.Should().BeOfType<Stack>().Subject;

        stack.Children[0].Should().BeOfType<Positioned>("the action is revealed, not pushed");
        stack.Children[1].Should().BeOfType<Draggable>();

        var draggable = (Draggable)stack.Children[1];
        draggable.Axis.Should().Be(DragAxis.Horizontal);
        draggable.Min.Should().Be(-SwipeableRow.ActionWidth);
        draggable.Max.Should().Be(0, "a row never swipes the other way");
    }

    [Fact]
    public void SwipeableRow_OpensPastHalfTheAction_AndSpringsBackShortOfIt()
    {
        bool? opened = null;
        var row = new SwipeableRow(new Text("Marcos"), "Dispute", Icons.Warning)
        {
            OnOpenChanged = value => opened = value,
        };
        var draggable = (Draggable)((Stack)((Box)row.Build(Ctx)).Child!).Children[1];

        draggable.OnReleased!(-60);
        opened.Should().BeTrue("past half the action's width it stays open");

        draggable.OnReleased!(-20);
        opened.Should().BeFalse("short of it, it springs back");
    }

    [Fact]
    public void SwipeableRow_WhenOpen_RestsAtTheActionsWidth()
    {
        var row = new SwipeableRow(new Text("Marcos"), "Dispute", Icons.Warning) { Open = true };
        var draggable = (Draggable)((Stack)((Box)row.Build(Ctx)).Child!).Children[1];

        draggable.RestOffset.Should().Be(-SwipeableRow.ActionWidth);
    }

    [Fact]
    public void PullToRefresh_AsksOnlyWhenThePullPassedTheThreshold()
    {
        var asked = 0;
        var pull = new PullToRefresh(new Text("List"), () => asked++);
        var draggable = (Draggable)((Stack)((Box)pull.Build(Ctx)).Child!).Children[1];

        draggable.Axis.Should().Be(DragAxis.Vertical);
        draggable.Min.Should().Be(0, "a list does not pull upward past its top");

        draggable.OnReleased!(PullToRefresh.Threshold - 1);
        asked.Should().Be(0, "a short pull asks for nothing");

        draggable.OnReleased!(PullToRefresh.Threshold);
        asked.Should().Be(1);
    }

    [Fact]
    public void SwitchThumb_DragsTowardTheEndItIsNotResting_On()
    {
        var off = new Switch(false);
        var thumb = (Draggable)((Positioned)((Stack)((Pressable)off.Build(Ctx)).Child).Children[1]).Child;
        thumb.Min.Should().Be(0, "an off switch never drags further off");
        thumb.Max.Should().Be(20, "52 track less the 26 thumb less both 3dp insets");

        var on = new Switch(true);
        var onThumb = (Draggable)((Positioned)((Stack)((Pressable)on.Build(Ctx)).Child).Children[1]).Child;
        onThumb.Min.Should().Be(-20);
        onThumb.Max.Should().Be(0);
    }

    [Fact]
    public void SwitchThumb_TogglesPastHalf_AndSaysNothingWhenItReturns()
    {
        var toggles = 0;
        var off = new Switch(false, () => toggles++);
        var thumb = (Draggable)((Positioned)((Stack)((Pressable)off.Build(Ctx)).Child).Children[1]).Child;

        thumb.OnReleased!(6);
        toggles.Should().Be(0, "short of half the slack the thumb returns and nothing changed");

        thumb.OnReleased!(14);
        toggles.Should().Be(1);
    }

    [Fact]
    public void SwitchThumb_WhenDisabled_IsNotDraggableAtAll()
    {
        var disabled = new Switch(true) { Disabled = true };
        ((Positioned)((Stack)((Pressable)disabled.Build(Ctx)).Child).Children[1]).Child
            .Should().BeOfType<Box>();
    }

    [Fact]
    public void SliderTrack_ScrubsRelativeToTheValue_Quantized()
    {
        var value = float.NaN;
        var slider = new Slider(20, v => value = v) { Min = 0, Max = 100, Step = 5 };
        var drag = (Draggable)((Box)slider.Build(Ctx)).Child!;

        drag.Normalized.Should().BeTrue("a fluid track has no pixel width the component can know");
        drag.Follows.Should().BeFalse("the value places the thumb; a translate would move it twice");
        drag.RestOffset.Should().BeApproximately(0.2f, 1e-4f, "the scrub starts where the value is");

        drag.OnMoved!(0.42f);
        value.Should().Be(40, "the scrub lands on the same values a press does");
    }

    [Fact]
    public void PullToRefresh_HoldsTheContentDownWhileTheWorkIsInFlight()
    {
        var pull = new PullToRefresh(new Text("List")) { Refreshing = true };
        var draggable = (Draggable)((Stack)((Box)pull.Build(Ctx)).Child!).Children[1];

        draggable.RestOffset.Should().Be(PullToRefresh.Threshold,
            "the spinner stays uncovered until the fetch lands");
    }
}
