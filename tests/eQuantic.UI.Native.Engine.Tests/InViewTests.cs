using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Presence, reported on the TRANSITIONS.
/// <para>
/// The question a table of contents asks is "which heading is the reader looking at", and the only
/// way to answer it was the page's scroll position — which lives on a ScrollView, so the article
/// had to be wrapped in one. That changes the scroll model of the whole page.
/// </para>
/// </summary>
public class InViewTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    // An InView reports in a tree that is drawn again: a live owner, kept across the frames of a test
    // the way a host keeps it, and where each InView's last answer lives.
    private static void Draw(VisualNode node, BuildOwner owner, float height = 100) =>
        PhotonRealizer.Realize(node, 200, height, Theme, ThemeMode.Light, new DisplayListBuilder(),
            owner: owner);

    private static Column Page(Action<bool> onChanged, float spacerHeight, float threshold = 0)
    {
        var column = new Column(gap: 0);
        column.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = spacerHeight }));
        column.Add(new InView(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 50 }), onChanged)
        {
            Threshold = threshold,
        });
        return column;
    }

    [Fact]
    public void AChildOnScreen_IsReported()
    {
        var seen = new List<bool>();
        Draw(Page(seen.Add, spacerHeight: 0), new BuildOwner());

        seen.Should().Equal(true);
    }

    /// <summary>Pushed past the surface, it is not — and a component learns that without asking.</summary>
    [Fact]
    public void AChildBelowTheSurface_IsNot()
    {
        var seen = new List<bool>();
        Draw(Page(seen.Add, spacerHeight: 400), new BuildOwner());

        seen.Should().BeEmpty("it never became visible, so nothing changed");
    }

    /// <summary>
    /// The TRANSITIONS only. A callback that fires once a frame is a callback every caller has to
    /// debounce, and a table of contents watching thirty headings would do it thirty times.
    /// </summary>
    [Fact]
    public void StayingVisible_DoesNotKeepFiring()
    {
        var seen = new List<bool>();
        var owner = new BuildOwner();
        var page = Page(seen.Add, spacerHeight: 0);

        Draw(page, owner);
        Draw(page, owner);
        Draw(page, owner);

        seen.Should().Equal(true);
    }

    [Fact]
    public void LeavingTheSurface_ReportsTheDeparture()
    {
        var seen = new List<bool>();
        var owner = new BuildOwner();

        Draw(Page(seen.Add, spacerHeight: 0), owner);
        Draw(Page(seen.Add, spacerHeight: 400), owner);

        seen.Should().Equal(true, false);
    }

    /// <summary>
    /// A tree laid out once reports nothing, as it runs no lifecycle hook: a callback that sets state
    /// for a tree nobody draws again sets it for nobody. A host's tree is drawn again.
    /// </summary>
    [Fact]
    public void ATreeLaidOutOnce_ReportsNothing()
    {
        var seen = new List<bool>();
        PhotonRealizer.Realize(Page(seen.Add, spacerHeight: 0), 200, 100, Theme, ThemeMode.Light,
            new DisplayListBuilder());

        seen.Should().BeEmpty();
    }

    /// <summary>A threshold asks for MORE than a sliver — a heading half off the top is not the one
    /// the reader is on.</summary>
    [Fact]
    public void AThreshold_WaitsUntilEnoughIsShowing()
    {
        var seen = new List<bool>();
        // 50 tall, 30 of it on screen: 60%, under a threshold of 0.9.
        Draw(Page(seen.Add, spacerHeight: 70, threshold: 0.9f), new BuildOwner());

        seen.Should().BeEmpty();

        var enough = new List<bool>();
        Draw(Page(enough.Add, spacerHeight: 0, threshold: 0.9f), new BuildOwner());
        enough.Should().Equal(true);
    }
}

/// <summary>
/// What counts as ON SCREEN narrows through a clip, the same way paint and input already do.
/// <para>
/// The comparison used to be against the whole surface, so a row scrolled out of a list still
/// counted as visible — a windowed list would load every item it had, which is the one job this
/// node exists to make cheap.
/// </para>
/// </summary>
public class InViewClipTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static void Draw(VisualNode node, BuildOwner owner) =>
        PhotonRealizer.Realize(node, 200, 100, Theme, ThemeMode.Light, new DisplayListBuilder(),
            owner: owner);

    // Explicit widths, not Fill: a column of all-Fill children inside a scroll view measures zero
    // wide, and a zero-area node is invisible for reasons that have nothing to do with clipping.
    private static Box Row(float height) => new(new BoxStyle { Width = 200, Height = height });

    private static VisualNode Watched(Action<bool> onChanged) => new InView(Row(50), onChanged);

    /// <summary>
    /// The window is 100 tall and the list's viewport is 60: a row at y=70 is INSIDE the window and
    /// outside the viewport. That gap is the whole test — a row that is off the window too would
    /// pass without any of this.
    /// </summary>
    [Fact]
    public void ARowScrolledOutOfItsList_IsNotOnScreen()
    {
        var seen = new List<bool>();
        var content = new Column(gap: 0);
        content.Add(Row(70));
        content.Add(Watched(seen.Add));

        Draw(new ScrollView(content) { Width = 200, Height = 60 }, new BuildOwner());

        seen.Should().BeEmpty("it is inside the window, and outside the list's viewport");
    }

    /// <summary>And one inside the viewport still is — the narrowing must not answer no to
    /// everything.</summary>
    [Fact]
    public void ARowInsideTheViewport_Is()
    {
        var seen = new List<bool>();
        var content = new Column(gap: 0);
        content.Add(Watched(seen.Add));
        content.Add(Row(300));

        Draw(new ScrollView(content) { Width = 200, Height = 60 }, new BuildOwner());

        seen.Should().Equal(true);
    }

    /// <summary>A clipping Box confines its children the same way a ScrollView does.</summary>
    [Fact]
    public void AChildClippedOutOfABox_IsNotOnScreen()
    {
        var seen = new List<bool>();
        var inner = new Column(gap: 0);
        inner.Add(Row(50));
        inner.Add(Watched(seen.Add));

        // Clipped to 40 but 100 of window below it: the row at y=50 is on screen and out of the box.
        Draw(new Box(new BoxStyle { Width = 200, Height = 40, Clip = true }, inner), new BuildOwner());

        seen.Should().BeEmpty();
    }

    /// <summary>Without any clip in between, the same row IS on screen — so the three tests above
    /// are about clipping and not about the row being off the window.</summary>
    [Fact]
    public void TheSameRow_WithoutAClip_IsOnScreen()
    {
        var seen = new List<bool>();
        var content = new Column(gap: 0);
        content.Add(Row(70));
        content.Add(Watched(seen.Add));

        Draw(content, new BuildOwner());

        seen.Should().Equal(true);
    }
}
