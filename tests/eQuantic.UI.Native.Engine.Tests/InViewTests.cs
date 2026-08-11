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

    private static void Draw(VisualNode node, InViewStore store, float height = 100) =>
        PhotonRealizer.Realize(node, 200, height, Theme, ThemeMode.Light, new DisplayListBuilder(),
            inViewStore: store);

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
        Draw(Page(seen.Add, spacerHeight: 0), new InViewStore());

        seen.Should().Equal(true);
    }

    /// <summary>Pushed past the surface, it is not — and a component learns that without asking.</summary>
    [Fact]
    public void AChildBelowTheSurface_IsNot()
    {
        var seen = new List<bool>();
        Draw(Page(seen.Add, spacerHeight: 400), new InViewStore());

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
        var store = new InViewStore();
        var page = Page(seen.Add, spacerHeight: 0);

        Draw(page, store);
        Draw(page, store);
        Draw(page, store);

        seen.Should().Equal(true);
    }

    [Fact]
    public void LeavingTheSurface_ReportsTheDeparture()
    {
        var seen = new List<bool>();
        var store = new InViewStore();

        Draw(Page(seen.Add, spacerHeight: 0), store);
        Draw(Page(seen.Add, spacerHeight: 400), store);

        seen.Should().Equal(true, false);
    }

    /// <summary>A threshold asks for MORE than a sliver — a heading half off the top is not the one
    /// the reader is on.</summary>
    [Fact]
    public void AThreshold_WaitsUntilEnoughIsShowing()
    {
        var seen = new List<bool>();
        // 50 tall, 30 of it on screen: 60%, under a threshold of 0.9.
        Draw(Page(seen.Add, spacerHeight: 70, threshold: 0.9f), new InViewStore());

        seen.Should().BeEmpty();

        var enough = new List<bool>();
        Draw(Page(enough.Add, spacerHeight: 0, threshold: 0.9f), new InViewStore());
        enough.Should().Equal(true);
    }
}
