using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A clip confines PIXELS and INPUT alike. Without that, a control scrolled out of the viewport
/// keeps taking taps: the region is still registered, it still sits topmost in dispatch order, and
/// it is drawn nowhere — so a fixed toolbar over a list goes dead the moment the list moves, and
/// the tap lands on a button nobody can see. Nothing errors, nothing looks wrong, and half the app
/// stops responding.
/// </summary>
public class ClippedInputTests
{
    /// <summary>A toolbar that does not scroll, over a list that does — the Studio's own anatomy.</summary>
    private sealed class ToolbarOverList : Primitives.StatefulComponent
    {
        public readonly List<string> Fired = [];

        public override VisualNode Build(ComponentContext context)
        {
            var body = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
            body.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 },
                new Button("Toolbar", onPressed: () => Fired.Add("Toolbar"))));

            var list = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            for (var i = 0; i < 20; i++)
            {
                var index = i;
                list.Add(new Button($"B{index}", onPressed: () => Fired.Add($"B{index}")));
            }
            body.Add(new Flexible(new ScrollView(list) { Width = SizeValue.Fill }, 1));
            return body;
        }
    }

    private static (ToolbarOverList Page, PhotonHost Host, RealizeResult Frame) Mount()
    {
        var page = new ToolbarOverList();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 390, 500);
        host.RenderFrame(new DisplayListBuilder());
        return (page, host, host.RenderFrame(new DisplayListBuilder(), 1000));
    }

    [Fact]
    public void AFixedToolbar_StillTakesItsOwnTaps_AfterTheListUnderItScrolls()
    {
        var (page, host, frame) = Mount();
        var toolbar = frame.HitRegions[0].Bounds.Center;

        host.PressDown(toolbar.X, toolbar.Y);
        host.PressUp(toolbar.X, toolbar.Y);
        page.Fired.Should().Equal(["Toolbar"], "nothing has scrolled yet");
        page.Fired.Clear();

        host.ScrollBy(200, 300, 400);
        host.RenderFrame(new DisplayListBuilder(), 1100);

        host.PressDown(toolbar.X, toolbar.Y);
        host.PressUp(toolbar.X, toolbar.Y);
        page.Fired.Should().Equal(["Toolbar"],
            "the row that scrolled up there is drawn nowhere, so it takes nothing");
    }

    [Fact]
    public void NoRegionSurvives_EntirelyOutsideItsScrollViewport()
    {
        var (_, host, _) = Mount();
        host.ScrollBy(200, 300, 400);
        var frame = host.RenderFrame(new DisplayListBuilder(), 1100);

        // The toolbar sits above the viewport legitimately — every OTHER region must intersect it.
        var viewport = frame.ScrollRegions.Single().Bounds;
        var stray = frame.HitRegions
            .Where(region => region.Bounds.Top >= viewport.Bottom || region.Bounds.Bottom <= viewport.Top)
            .Where(region => region.Bounds.Bottom > 56)
            .ToList();
        stray.Should().BeEmpty();
    }

    [Fact]
    public void ARowStraddlingTheEdge_KeepsOnlyThePartYouCanSee()
    {
        var (_, host, _) = Mount();
        host.ScrollBy(200, 300, 30);   // a partial row at the viewport's top edge
        var frame = host.RenderFrame(new DisplayListBuilder(), 1100);
        var viewport = frame.ScrollRegions.Single().Bounds;

        foreach (var region in frame.HitRegions.Where(r => r.Bounds.Bottom > 56))
        {
            region.Bounds.Top.Should().BeGreaterThanOrEqualTo(viewport.Top - 0.01f);
            region.Bounds.Bottom.Should().BeLessThanOrEqualTo(viewport.Bottom + 0.01f);
        }
    }

    /// <summary>A scrolling list of links, and under it a footer band that does not scroll.</summary>
    private sealed class LinksOverFooter : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var body = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
            var list = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            for (var i = 0; i < 20; i++)
                list.Add(new Link($"/p{i}", new Text($"page {i}", TypeRole.Label)));
            body.Add(new Flexible(new ScrollView(list) { Width = SizeValue.Fill }, 1));
            body.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 },
                new Text("footer", TypeRole.Label)));
            return body;
        }
    }

    /// <summary>
    /// A LINK'S IDENTITY OUTLIVES ITS VISIBILITY; ITS RECTANGLE DOES NOT. Following a link is not a
    /// pointer act — the keyboard and a reader's activate find it by PATH — and a focus stop is
    /// deliberately kept when it scrolls away. The link REGION was dropped instead, so the two
    /// halves disagreed the moment a list was longer than its viewport:
    /// <code>
    /// STOPS  r/0/0 … r/0/19   (20)
    /// LINKS  r/0/0 … r/0/4    (5)
    /// ActivatePath("r/0/19") = True, NavigationRequested never called
    /// </code>
    /// True and nowhere is the worst answer available: a reader reports the link as followed.
    /// <para>
    /// The rectangle still obeys the clip, and that is the second half here: a link entirely
    /// outside intersects to ZERO AREA, and no point is inside a rect whose left edge is its right
    /// one — so the footer drawn over where that link would have been keeps taking its own taps.
    /// </para>
    /// <para>Mutation: register a link region only when it is visible and the first half fails
    /// (nothing followed); register it unclipped and the second fails (a tap on the footer band
    /// follows the link that scrolled under it).</para>
    /// </summary>
    [Fact]
    public void AnOffScreenLinkIsFollowableByPath_AndStillTakesNoTap()
    {
        var host = new PhotonHost(new LinksOverFooter(), PhotonTheme.Instance, ThemeMode.Light, 390, 220);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);

        frame.LinkRegions.Select(region => region.Destination).Should()
            .BeEquivalentTo(Enumerable.Range(0, 20).Select(index => $"/p{index}"),
                "all twenty keep a destination though four fit on screen — a stop with no region "
                + "is an activate that returns true and goes nowhere");

        string? followed = null;
        host.NavigationRequested = destination => followed = destination;

        var offScreen = frame.LinkRegions.Single(region => region.Destination == "/p19");
        host.ActivatePath(offScreen.Path).Should().BeTrue();
        followed.Should().Be("/p19", "the twentieth link is below the viewport and still has a destination");

        // A point in the FOOTER's band, which the list scrolls under: without the clip it is the
        // link at that coordinate that a tap there follows.
        followed = null;
        host.PressDown(195, 200);
        host.PressUp(195, 200);
        followed.Should().BeNull("a link clipped out of the viewport is drawn nowhere, so it is tapped nowhere");
    }
}

/// <summary>
/// A Pressable AROUND a control — the Menu/trigger shape — is ordinary composition. On the web it
/// needs the outer element to yield (HTML forbids a button inside a button); Photon draws rather
/// than marking up, so BOTH regions exist and the topmost one wins. This pins that the inner
/// control keeps its own press, which is what makes the two targets agree about behaviour.
/// </summary>
public class NestedPressableTests
{
    private sealed class TriggerInsideAWrapper : Primitives.StatefulComponent
    {
        public readonly List<string> Fired = [];

        public override VisualNode Build(ComponentContext context) =>
            new Pressable(new Button("Open", onPressed: () => Fired.Add("inner")),
                () => Fired.Add("outer"));
    }

    [Fact]
    public void TheINNERControl_TakesThePress()
    {
        var page = new TriggerInsideAWrapper();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 390, 200);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);

        var centre = frame.HitRegions[^1].Bounds.Center;
        host.PressDown(centre.X, centre.Y);
        host.PressUp(centre.X, centre.Y);

        page.Fired.Should().Equal(["inner"], "the topmost region is the control itself");
    }
}


/// <summary>
/// A press begins in one frame and ends in another — always. Showing the pressed state repaints,
/// and the next Build hands back FRESH nodes, so a target remembered by object identity is already
/// gone when the finger lifts. Nothing errors: the control lights up, the release finds nothing,
/// and the handler never runs. A synthetic click that never moves and never spans a frame passes,
/// which is exactly how this survived every self-test while no human could click anything.
/// </summary>
public class PressAcrossFramesTests
{
    private sealed class OneButton : Primitives.StatefulComponent
    {
        public readonly List<string> Fired = [];

        public override VisualNode Build(ComponentContext context) =>
            new Button("Continue", onPressed: () => Fired.Add("pressed"));
    }

    private static (OneButton Page, PhotonHost Host, Point Centre) Mount()
    {
        var page = new OneButton();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 390, 200);
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder(), 1000);
        return (page, host, frame.HitRegions[^1].Bounds.Center);
    }

    [Fact]
    public void APress_ThatSpansAFrame_StillFires()
    {
        var (page, host, centre) = Mount();

        host.PressDown(centre.X, centre.Y);
        host.RenderFrame(new DisplayListBuilder(), 1016);   // the pressed state paints
        host.PressUp(centre.X, centre.Y);

        page.Fired.Should().Equal(["pressed"]);
    }

    [Fact]
    public void APress_ThatWANDERSAndComesBack_StillFires()
    {
        var (page, host, centre) = Mount();

        host.PressDown(centre.X, centre.Y);
        host.PointerMove(centre.X + 2, centre.Y + 2);       // no hand holds perfectly still
        host.RenderFrame(new DisplayListBuilder(), 1016);
        host.PressUp(centre.X + 2, centre.Y + 2);

        page.Fired.Should().Equal(["pressed"]);
    }

    [Fact]
    public void AReleaseOUTSIDE_StillCancels()
    {
        var (page, host, centre) = Mount();

        host.PressDown(centre.X, centre.Y);
        host.RenderFrame(new DisplayListBuilder(), 1016);
        host.PressUp(centre.X + 400, centre.Y + 400);

        page.Fired.Should().BeEmpty("a press abandoned off the control is not a press");
    }

}
