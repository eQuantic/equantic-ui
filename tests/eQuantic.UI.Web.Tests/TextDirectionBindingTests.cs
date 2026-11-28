using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What only the SDK draws or hears follows the direction (#1569), on the server's half: a glyph that
/// points along the reading is turned through <c>--eq-inline</c>, a horizontal drag's rest is written
/// through it, the horizontal arrows are previous and next by the direction where the element reads, and a
/// code diff builds left to right. The browser's half is <c>lowering.ts</c> and its specs; the parity
/// fixture holds the two producers to the same markup, and a handler is a name there, so what each
/// declared handler DOES is asserted here, by invoking it.
/// </summary>
public class TextDirectionBindingTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private const string Turned = "scale: var(--eq-inline, 1) 1";

    private static T InCulture<T>(string name, Func<T> read)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        try
        {
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static string StyleOf(HtmlNode node) => node.Attributes.TryGetValue("style", out var style) ? style ?? "" : "";

    private static Directionality Reading(TextDirection direction, VisualNode child) => new(direction, child);

    // ---- glyphs -------------------------------------------------------------------------------

    [Fact]
    public void OnlyTheSidewaysCuratedGlyphs_MatchTheDirection()
    {
        // Derived from the curated set: a glyph that starts to match, or one that stops, shows here.
        Enum.GetValues<Icons>().Where(glyph => CuratedIcons.Resolve(glyph).MatchTextDirection)
            .Should().BeEquivalentTo([Icons.ChevronLeft, Icons.ChevronRight],
                "Play is media and keeps its direction, as Flutter's play_arrow does");
    }

    [Theory]
    [InlineData(TextDirection.LeftToRight)]
    [InlineData(TextDirection.RightToLeft)]
    public void AGlyphThatMatchesTheDirection_IsTurnedThroughTheInlineSign_InBothDirections(TextDirection direction)
    {
        // The same markup either way: the sign is a custom property the nearest [dir] sets, so the glyph
        // turns, stays put inside a dir="ltr" surface and follows a culture switch with no new build.
        StyleOf(Render(Reading(direction, new Icon(Icons.ChevronLeft)))).Should().Contain(Turned);
        StyleOf(Render(Reading(direction, new Icon(Icons.ChevronRight)))).Should().Contain(Turned);
        StyleOf(Render(Reading(direction, new Vector(CuratedIcons.Resolve(Icons.ChevronLeft), 24)))).Should().Contain(Turned,
            "a vector draws the same glyph data, and mirrors with it");
    }

    [Fact]
    public void AGlyphThatDoesNotMatch_IsDrawnAsBefore()
    {
        StyleOf(Render(Reading(TextDirection.RightToLeft, new Icon(Icons.Play)))).Should().NotContain("scale");
        StyleOf(Render(Reading(TextDirection.RightToLeft, new Icon(new IconGlyph("arrow", "M4 12h16"))))).Should().NotContain("scale");
    }

    [Fact]
    public void APackGlyphAnAppMarks_IsTurned()
    {
        var marked = new IconGlyph("arrow", "M4 12h16") with { MatchTextDirection = true };

        StyleOf(Render(new Icon(marked))).Should().Contain(Turned);
    }

    [Fact]
    public void TheMarkupOfAMatchingGlyph_DoesNotDependOnTheCulture()
    {
        var icon = new Icon(Icons.ChevronRight);

        InCulture("ar", () => HtmlRenderer.RenderNode(Render(icon)))
            .Should().Be(InCulture("en", () => HtmlRenderer.RenderNode(Render(icon))));
    }

    // ---- a drag's rest ------------------------------------------------------------------------

    private static string RestOf(Draggable draggable) => StyleOf(Render(draggable));

    private static Draggable Dragged(DragAxis axis, float rest, bool normalized = false) =>
        new(new Box(new BoxStyle { Width = 40, Height = 40 }), _ => { })
        {
            Axis = axis, Min = -100, Max = 100, RestOffset = rest, Normalized = normalized,
        };

    [Fact]
    public void AHorizontalRest_IsWrittenTowardTheEnd_InPixelsAndNormalized()
    {
        RestOf(Dragged(DragAxis.Horizontal, -96)).Should().Contain("translate: calc(var(--eq-inline, 1) * -96px)");
        RestOf(Dragged(DragAxis.Horizontal, 0.25f, normalized: true)).Should().Contain("translate: calc(var(--eq-inline, 1) * 25%)");
    }

    [Fact]
    public void AVerticalRest_IsPhysicalAsItWas()
    {
        RestOf(Dragged(DragAxis.Vertical, 40)).Should().Contain("translate: 0 40px").And.NotContain("--eq-inline");
        RestOf(Dragged(DragAxis.Vertical, 0.5f, normalized: true)).Should().Contain("translate: 0 50%").And.NotContain("--eq-inline");
    }

    [Fact]
    public void ARestAtZero_WritesNoTranslate()
    {
        RestOf(Dragged(DragAxis.Horizontal, 0)).Should().NotContain("translate:");
    }

    // ---- the arrows ---------------------------------------------------------------------------

    /// <summary>Every element of a lowered tree, the root first.</summary>
    private static IEnumerable<HtmlElement> Elements(HtmlElement root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<HtmlElement>())
            foreach (var found in Elements(child))
                yield return found;
    }

    private static HtmlElement HostWithRole(HtmlElement root, string role, int index = 0) =>
        Elements(root).Where(element => element is RealizedElement { RawAttributes: { } raw }
                                        && raw.TryGetValue("role", out var value) && value == role)
            .ElementAt(index);

    /// <summary>The adjustments the declared keydown of <paramref name="host"/> makes for <paramref name="key"/>.</summary>
    private static List<int> Adjusted(HtmlElement host, List<int> log, string key)
    {
        log.Clear();
        host.OnKeyDown.Should().NotBeNull("the server declares the keydown the browser installs");
        host.OnKeyDown!(new KeyboardEventArgs { Key = key });
        return [.. log];
    }

    private static Adjustable Slider(List<int> log, AdjustableRole role = AdjustableRole.Slider) =>
        new(new Text("x", TypeRole.BodyM), log.Add)
        {
            Role = role,
            Value = role is AdjustableRole.Slider or AdjustableRole.Separator ? new RangeValue(0.5f, 0, 1) : null,
        };

    [Theory]
    [InlineData(TextDirection.LeftToRight, "ArrowRight", 1)]
    [InlineData(TextDirection.LeftToRight, "ArrowLeft", -1)]
    [InlineData(TextDirection.RightToLeft, "ArrowLeft", 1)]
    [InlineData(TextDirection.RightToLeft, "ArrowRight", -1)]
    public void AnAdjustables_HorizontalArrows_AreNextAndPreviousByTheDirection(TextDirection direction, string key, int expected)
    {
        var log = new List<int>();
        var lowered = WebRealizer.Lower(Reading(direction, Slider(log)), Theme);

        Adjusted(HostWithRole(lowered, "slider"), log, key).Should().Equal(expected);
    }

    [Theory]
    [InlineData(TextDirection.LeftToRight, AdjustableRole.Slider, "ArrowUp", 1)]
    [InlineData(TextDirection.RightToLeft, AdjustableRole.Slider, "ArrowUp", 1)]
    [InlineData(TextDirection.RightToLeft, AdjustableRole.Slider, "ArrowDown", -1)]
    [InlineData(TextDirection.LeftToRight, AdjustableRole.Tablist, "ArrowDown", 1)]
    [InlineData(TextDirection.RightToLeft, AdjustableRole.Tablist, "ArrowDown", 1)]
    [InlineData(TextDirection.RightToLeft, AdjustableRole.Tablist, "ArrowUp", -1)]
    public void TheVerticalArrows_DoNotReadTheDirection(TextDirection direction, AdjustableRole role, string key, int expected)
    {
        var log = new List<int>();
        var lowered = WebRealizer.Lower(Reading(direction, Slider(log, role)), Theme);

        Adjusted(HostWithRole(lowered, role == AdjustableRole.Tablist ? "tablist" : "slider"), log, key).Should().Equal(expected);
    }

    [Theory]
    [InlineData("ar", "ArrowLeft", 1)]
    [InlineData("ar", "ArrowRight", -1)]
    [InlineData("en", "ArrowRight", 1)]
    [InlineData("en", "ArrowLeft", -1)]
    public void WithNoDirectionality_TheCulturesDirectionIsTheRoots(string culture, string key, int expected)
    {
        var log = new List<int>();
        var lowered = InCulture(culture, () => WebRealizer.Lower(Slider(log), Theme));

        Adjusted(HostWithRole(lowered, "slider"), log, key).Should().Equal(expected);
    }

    /// <summary>A component that builds an <see cref="Adjustable"/>: a direction set above it has to reach
    /// what it builds, which the visitor lowers inside the call that expanded the component.</summary>
    private sealed class Tabs(List<int> log) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Slider(log);
    }

    [Fact]
    public void TheDirection_SurvivesAComponentBoundary_InADrawnOnceTree()
    {
        // The open question of the design: the visitor's direction is a field of the one visitor that
        // lowers the whole tree, and a component's build is lowered inside the call that expanded it.
        var log = new List<int>();
        var lowered = InCulture("en", () => WebRealizer.Lower(
            Reading(TextDirection.RightToLeft, new Column { new Tabs(log) }), Theme));

        Adjusted(HostWithRole(lowered, "slider"), log, "ArrowLeft").Should().Equal(1);
        Adjusted(HostWithRole(lowered, "slider"), log, "ArrowRight").Should().Equal(-1);
    }

    [Fact]
    public void TheDirection_SurvivesAComponentBoundary_InAFrameOfALiveTree()
    {
        // The server draws a page through a host with positions (MarkupHost), whose frame lowers what each
        // position built: the same visitor, the same field.
        var log = new List<int>();
        var host = new MarkupHost(Theme);
        host.Build(Reading(TextDirection.RightToLeft, new Column { new Tabs(log), Reading(TextDirection.LeftToRight, new Tabs(log)) }));
        var lowered = host.Lower();
        host.EndFrame();
        host.Owner.UnmountAll();

        Adjusted(HostWithRole(lowered, "slider", 0), log, "ArrowLeft").Should().Equal([1], "under the page's right to left");
        Adjusted(HostWithRole(lowered, "slider", 1), log, "ArrowLeft").Should().Equal([-1], "under the nested left to right");
    }

    [Fact]
    public void AnAdaptiveNodesArm_ReadsTheDirectionItSitsIn()
    {
        var log = new List<int>();
        var lowered = WebRealizer.Lower(Reading(TextDirection.RightToLeft, new AdaptiveNode(Slider(log))), Theme);

        Adjusted(HostWithRole(lowered, "slider"), log, "ArrowLeft").Should().Equal(1);
    }

    [Fact]
    public void ANestedDirectionality_IsRestoredWhereItEnds()
    {
        // Two siblings under a right to left page, the first inside a left to right subtree: the fence
        // must not leak past its end, or the second would read left to right too.
        var log = new List<int>();
        var lowered = WebRealizer.Lower(Reading(TextDirection.RightToLeft, new Row
        {
            Reading(TextDirection.LeftToRight, Slider(log)),
            Slider(log),
        }), Theme);

        Adjusted(HostWithRole(lowered, "slider", 0), log, "ArrowRight").Should().Equal([1], "inside the left to right subtree");
        Adjusted(HostWithRole(lowered, "slider", 1), log, "ArrowLeft").Should().Equal([1], "after it, the page's own direction is back");
    }

    [Fact]
    public void ATableViewportCell_ReadsLeftToRight_InAnArabicPage()
    {
        // The viewport writes dir="ltr" on its own element, so what its cells hold reads left to right and
        // the server's declared handler names the direction the browser's reads at the key.
        var log = new List<int>();
        var table = new TableView(new UniformTableSpans(1, 28), new UniformTableSpans(1, 96), (_, _) => Slider(log));

        var lowered = InCulture("ar", () => WebRealizer.Lower(Reading(TextDirection.RightToLeft, table), Theme));

        Adjusted(HostWithRole(lowered, "slider"), log, "ArrowRight").Should().Equal(1);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("sheet")]
    public void ASurfacesContent_ReadsLeftToRight_InAnArabicPage(string surface)
    {
        // Each of the three hosts that write dir="ltr" on themselves says so to the visitor too.
        var log = new List<int>();
        VisualNode host = surface == "code"
            ? new CodeSurface(Slider(log), new eQuantic.UI.Code.CodeEditorController("x", eQuantic.UI.Code.CodeLanguages.CSharp))
            : new SheetSurface(Slider(log), new eQuantic.UI.Sheet.SheetController(2, 2));

        var lowered = InCulture("ar", () => WebRealizer.Lower(Reading(TextDirection.RightToLeft, host), Theme));

        Adjusted(HostWithRole(lowered, "slider"), log, "ArrowRight").Should().Equal([1]);
    }

    // ---- a navigable's keydown ----------------------------------------------------------------

    private static Navigable Grid(List<NavigableMove> moves) =>
        new(moves.Add, [new Row { new Text("a", TypeRole.BodyM), new Text("b", TypeRole.BodyM) }]) { Label = "days" };

    private static List<NavigableMove> Pressed(HtmlElement lowered, List<NavigableMove> moves, string key)
    {
        moves.Clear();
        var host = HostWithRole(lowered, "grid");
        host.OnKeyDown.Should().NotBeNull();
        host.OnKeyDown!(new KeyboardEventArgs { Key = key });
        return [.. moves];
    }

    [Theory]
    [InlineData(TextDirection.LeftToRight, "ArrowLeft", NavigableMove.PreviousItem)]
    [InlineData(TextDirection.LeftToRight, "ArrowRight", NavigableMove.NextItem)]
    [InlineData(TextDirection.RightToLeft, "ArrowLeft", NavigableMove.NextItem)]
    [InlineData(TextDirection.RightToLeft, "ArrowRight", NavigableMove.PreviousItem)]
    [InlineData(TextDirection.RightToLeft, "ArrowDown", NavigableMove.NextRow)]
    [InlineData(TextDirection.RightToLeft, "Home", NavigableMove.RowStart)]
    public void ANavigablesKeys_AreTheTablesAnswerForTheDirectionItSitsIn(TextDirection direction, string key, NavigableMove expected)
    {
        var moves = new List<NavigableMove>();
        var lowered = WebRealizer.Lower(Reading(direction, Grid(moves)), Theme);

        Pressed(lowered, moves, key).Should().Equal(expected);
    }

    [Fact]
    public void ANavigable_ReadsTheCulturesDirection_WithNoDirectionality()
    {
        var moves = new List<NavigableMove>();
        var lowered = InCulture("ar", () => WebRealizer.Lower(Grid(moves), Theme));

        Pressed(lowered, moves, "ArrowLeft").Should().Equal(NavigableMove.NextItem);
    }

    // ---- the catalog --------------------------------------------------------------------------

    [Fact]
    public void ACodeDiff_BuildsLeftToRight_InAnArabicPage()
    {
        // The one code view #1568 left unfenced: its gutters, its two sides and their horizontal scrolls
        // stand where the grid measured them, from the left, so the whole diff reads left to right.
        var diff = InCulture("ar", () => Render(new CodeDiff("a\nb", "a\nc", "plaintext")));

        diff.Attributes.Should().ContainKey("dir").WhoseValue.Should().Be("ltr");
    }
}
