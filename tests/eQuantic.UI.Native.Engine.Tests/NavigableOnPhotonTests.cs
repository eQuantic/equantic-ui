using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A two-dimensional composite on the target that used to have none of it (#248). The node's own doc
/// says what one IS — "one Tab stop for the whole thing, and a keyboard that moves a selection around
/// inside it" — and Photon had neither half: <c>MeasureVisitor.Visit(Navigable)</c> measured the node
/// and none of its <c>Rows</c>, so a calendar was a grid with no days in it, and
/// <c>Navigable.OnMove</c> was wired on the web alone.
/// <para>
/// Measured against a real <see cref="Calendar"/> rather than a hand-built grid, because the three
/// halves only meet in one: the rows have to lay out for the cells to be announced, the cells have to
/// be suppressed for the grid to be one stop, and the stop has to carry the composite for the arrows
/// to reach it.
/// </para>
/// </summary>
public class NavigableOnPhotonTests
{
    private static (PhotonHost Host, RealizeResult Frame, int Commands) Open()
    {
        var host = new PhotonHost(new Calendar(selected: new DateOnly(2026, 7, 14)),
            PhotonTheme.Instance, ThemeMode.Light, 420, 520);
        var builder = new DisplayListBuilder();
        var frame = host.RenderFrame(builder);
        return (host, frame, builder.Build().Commands.Length);
    }

    /// <summary>
    /// The rows lay out, so the days exist at all — to the reader, to a finger and to the painter.
    /// Before this a month announced its NAME over an empty subtree: the group #187 gave it, and
    /// nothing inside.
    /// </summary>
    [Fact]
    public void TheDaysReachTheReaderTheFingerAndThePainter()
    {
        var (host, frame, commands) = Open();
        var semantics = host.Semantics();

        semantics.Where(node => node.Role == SemanticRole.GridCell).Should().HaveCountGreaterThan(27,
            "a month has at least twenty-eight days and every one is a cell");
        semantics.Should().Contain(node => node.Role == SemanticRole.Group,
            "the composite still says its own name before the reader walks into it");
        semantics.Should().Contain(node => node.Label == "Sun",
            "the header row is announced too — it is a row of the grid, not decoration");

        frame.HitRegions.Should().HaveCountGreaterThan(27, "a day is something you tap");
        frame.Root.Bounds.Height.Should().BeGreaterThan(200, "the grid takes the space its rows need");
        commands.Should().BeGreaterThan(30, "and it paints them");
    }

    /// <summary>
    /// ONE stop, which is the other half of what the node is. Laying the rows out without this
    /// traded a grid nobody could see for 31 Tab presses — measured, and the reason the emit arm
    /// suppresses what is underneath in the same breath that it registers the composite's own stop,
    /// exactly as <see cref="Adjustable"/> has always done.
    /// </summary>
    [Fact]
    public void TheWholeGridIsOneTabStopAndTheDaysAreNotStops()
    {
        var (_, frame, _) = Open();

        var grid = frame.FocusStops.Should().ContainSingle(stop => stop.Grid != null).Which;
        frame.FocusStops.Where(stop => stop.Path.StartsWith(grid.Path + "/")).Should().BeEmpty(
            "a month must not be 31 Tab presses");
        frame.FocusStops.Should().HaveCount(3,
            "the two month buttons and the grid — nothing else in a calendar takes the keyboard");
    }

    /// <summary>
    /// And the keyboard the stop promises actually moves the grid: a page turns the month, which is
    /// the one move whose effect is visible from outside the composite. A key the grid does not claim
    /// travels on, so the page keeps its Tab.
    /// </summary>
    [Fact]
    public void TheArrowsWalkTheGridAndTabStillTravels()
    {
        var (host, frame, _) = Open();
        var grid = frame.FocusStops.Single(stop => stop.Grid is not null);

        while (host.FocusedPath != grid.Path)
            host.FocusNext().Should().BeTrue("the grid is reachable by Tab");

        host.KeyDown("ArrowRight").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        host.KeyDown("ArrowDown").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());

        host.Semantics()[0].Label.Should().Be("July 2026", "walking a day does not page the month");

        host.KeyDown("PageDown").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        host.Semantics()[0].Label.Should().Be("August 2026", "a page DOES");

        host.KeyDown("Tab").Should().BeTrue("and the grid never swallowed Tab");
        host.FocusedPath.Should().NotBe(grid.Path);
    }
}
