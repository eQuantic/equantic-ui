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

        // The header row is announced too — it is a row of the grid, not decoration. Counted rather
        // than named: the day names come from the CULTURE (the component's own doc says so), and a
        // test that asserted "Sun" passed here and failed on a CI runner whose locale answered
        // "日" — which is the component being right and the assertion being parochial.
        var grid = frame.FocusStops.Single(stop => stop.Grid != null).Path;
        semantics.Where(node => node.Role == SemanticRole.StaticText && node.Path.StartsWith(grid + "/"))
            .Should().HaveCountGreaterThanOrEqualTo(7, "seven day names, in whatever language");

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
    /// ACTIVATING the grid focuses it and leaves its keyboard working — which is the whole of what
    /// activating a composite can mean, and what the macOS and iOS bridges do to every element they
    /// expose. Found in review: <c>ActivatePath</c> asked by ELIMINATION which stops take the
    /// keyboard ("not a text field, so a surface"), and the composite stop this PR adds carries
    /// neither, so activating a calendar put its own path in <c>_textPath</c>, left
    /// <c>CodeTarget</c>, <c>SheetTarget</c> and <c>TextTarget</c> all null, and killed the arrows.
    /// Measured before the fix:
    /// <code>
    /// ActivatePath(grid) => True
    ///   FocusedPath=r/0/1  HasTextFocus=True  CodeTarget=null  SheetTarget=null
    ///   PageDown after activate => False
    /// </code>
    /// The question is asked by what the stop IS now, so a kind nobody has invented yet arrives and
    /// nothing more — which is the right default.
    /// </summary>
    [Fact]
    public void ActivatingTheGridLeavesItsKeyboardWorking()
    {
        var (host, frame, _) = Open();
        var grid = frame.FocusStops.Single(stop => stop.Grid != null).Path;

        host.ActivatePath(grid).Should().BeTrue();
        host.FocusedPath.Should().Be(grid);
        host.HasTextFocus.Should().BeFalse("a grid is not an editing surface and never takes the caret");

        var month = host.Semantics()[0].Label;
        host.KeyDown("PageDown").Should().BeTrue("the arrows still reach the composite");
        host.RenderFrame(new DisplayListBuilder());
        host.Semantics()[0].Label.Should().NotBe(month);
    }

    /// <summary>
    /// A row that says <see cref="VisualNode.Key"/> keeps its identity, which is the rule every other
    /// multi-child arm follows and this one missed on its first pass: the path IS identity on Photon
    /// — focus, hover, a scroll offset, a drag in flight are all remembered by it — so a grid whose
    /// rows are rebuilt or reordered under a positional path hands the ring to the row that took the
    /// index. Found in review, and the fix is the keyed <c>ChildPath</c> overload the flex, stack and
    /// grid passes already use.
    /// </summary>
    [Fact]
    public void AKeyedRowKeepsItsIdentity()
    {
        VisualNode Row(string key, string text)
        {
            var row = new Row(gap: 0) { Key = key };
            row.Add(new Text(text, TypeRole.BodyM));
            return row;
        }

        var reordered = new Navigable(_ => { }, [Row("b", "second"), Row("a", "first")]) { Label = "Weeks" };
        var host = new PhotonHost(reordered, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        var rows = frame.Root.Children.Select(child => child.Path).ToList();
        rows.Should().Equal(["r/[b]", "r/[a]"],
            "a keyed row takes the key as its segment, so its identity survives the reorder that "
            + "would have renamed a positional one");
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

        // The month's own title, whatever the culture spells it — read rather than named, for the
        // reason above. What the test is about is that one move changes it and the other does not.
        var month = host.Semantics()[0].Label;
        month.Should().NotBeEmpty();

        host.KeyDown("ArrowRight").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        host.KeyDown("ArrowDown").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());

        host.Semantics()[0].Label.Should().Be(month, "walking a day does not page the month");

        host.KeyDown("PageDown").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        host.Semantics()[0].Label.Should().NotBe(month, "a page DOES");

        host.KeyDown("Tab").Should().BeTrue("and the grid never swallowed Tab");
        host.FocusedPath.Should().NotBe(grid.Path);
    }
}
