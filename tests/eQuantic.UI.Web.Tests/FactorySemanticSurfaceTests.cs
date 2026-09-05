using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The DECLARATIVE surface can state semantics — pinned end to end.
/// <para>
/// The OS Cleaner's F1 report (the friction ledger in <c>docs/DESKTOP-PLAN.md</c>): factories
/// mirror constructors, <c>Label</c>/<c>Selected</c> were init-only, so a fully declarative screen
/// could not name an icon-only button or mark a nav item selected — the app stayed imperative
/// because of it. The fix widened the FACTORY pair (C# here, the runtime's <c>UI.ts</c> twin, and
/// the transpiled pin) rather than the constructor, because the constructor's trailing slot is
/// where the compiler lands object initializers — reshaping it would have broken every emitted
/// initializer. These tests author nodes EXACTLY as a declarative screen does (<c>using static</c>
/// factories, no <c>new</c>, no initializers) and assert the semantics reach the rendered HTML.
/// </para>
/// </summary>
public class FactorySemanticSurfaceTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Rendered(VisualNode node, string attribute) =>
        Walk(WebRealizer.Lower(node, Theme).Render())
            .First(candidate => candidate.Attributes.ContainsKey(attribute));

    [Fact]
    public void PressableFactoryStatesNameAndSelection()
    {
        var node = Pressable(Icon(Icons.Search), onPressed: () => { },
            label: "Search", selected: true);

        var button = Rendered(node, "aria-pressed");
        button.Attributes["aria-pressed"].Should().Be("true");
        button.Attributes["aria-label"].Should().Be("Search");
    }

    [Fact]
    public void PressableFactoryStatesDisclosureAndDisabled()
    {
        var expanded = Rendered(
            Pressable(Icon(Icons.Search), () => { }, expanded: false), "aria-expanded");
        expanded.Attributes["aria-expanded"].Should().Be("false",
            "closed is a statement — only a null Expanded means no disclosure at all");

        var disabled = Rendered(
            Pressable(Icon(Icons.Search), () => { }, label: "Search", disabled: true), "disabled");
        disabled.Attributes.Should().ContainKey("disabled");
    }

    [Fact]
    public void PressableFactoryCarriesThePressedFill()
    {
        var node = Pressable(Icon(Icons.Search), () => { },
            pressedBackground: Theme.SurfaceSubtle);

        var button = Walk(WebRealizer.Lower(node, Theme).Render())
            .First(candidate => candidate.Attributes.TryGetValue("style", out var style) &&
                                style.Contains("--eq-pressed-bg"));
        button.Attributes["class"].Should().Contain("eq-pressable");
    }

    [Fact]
    public void LinkFactoryStatesNameAndWhereYouAre()
    {
        var node = Link("/scan", Icon(Icons.Search), label: "Scan", current: true);

        var anchor = Rendered(node, "aria-current");
        anchor.Attributes["aria-current"].Should().Be("page");
        anchor.Attributes["aria-label"].Should().Be("Scan");
    }

    [Fact]
    public void TextEntryFactoryStatesNameHintAndObscuring()
    {
        var node = TextEntry("", label: "Password", placeholder: "Enter password",
            obscure: true, disabled: true);

        var input = Rendered(node, "placeholder");
        input.Attributes["type"].Should().Be("password");
        input.Attributes["placeholder"].Should().Be("Enter password");
        input.Attributes["aria-label"].Should().Be("Password");
        input.Attributes.Should().ContainKey("disabled");
    }
}

/// <summary>
/// The LAYOUT tail — the OS Cleaner's report after migrating its whole UI to the declarative surface:
/// 49 places could not say a container's width or height (init-only on VisualNode, no factory
/// parameter), so `new Row(...) { Width = SizeValue.Fill }` stayed the only spelling of the most
/// common layout there is. And 3 places needed a Pressable's composite role.
/// </summary>
public class FactoryLayoutTailTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void EveryContainerFactory_ReachesWidthAndHeight()
    {
        Row(width: SizeValue.Fill).Width.Should().Be(SizeValue.Fill);
        Column(height: SizeValue.Fill).Height.Should().Be(SizeValue.Fill);
        Grid([GridTrack.Flex()], width: SizeValue.Fill).Width.Should().Be(SizeValue.Fill);
        var stack = Stack(width: SizeValue.Fixed(320), height: SizeValue.Fixed(200));
        stack.Width.Should().Be(SizeValue.Fixed(320));
        stack.Height.Should().Be(SizeValue.Fixed(200));
        ScrollView(Text("x"), height: SizeValue.Fill).Height.Should().Be(SizeValue.Fill);
        ListView(3, 44, i => Text($"{i}"), width: SizeValue.Fill).Width.Should().Be(SizeValue.Fill);
    }

    [Fact]
    public void LeavingTheTailOut_MeansHug_ExactlyAsAnInitializerWould()
    {
        // default(SizeValue) is Hug: the factory without a width is the constructor without one.
        Row().Width.Should().Be(new Row(gap: 0).Width);
        Column(children: [Text("a")]).Height.Should().Be(SizeValue.Hug);
    }

    [Fact]
    public void PressableFactoryStatesItsCompositeRole()
    {
        var node = Pressable(Text("Overview"), () => { }, selected: true, role: PressableRole.Radio);
        var html = WebRealizer.Lower(node, Theme).Render();
        IEnumerable<HtmlNode> Walk(HtmlNode n) { yield return n; foreach (var c in n.Children) foreach (var d in Walk(c)) yield return d; }
        var radio = Walk(html).First(n => n.Attributes.TryGetValue("role", out var role) && role == "radio");
        radio.Attributes["aria-checked"].Should().Be("true", "a radio states its selection as checked, not pressed");
    }
}

/// <summary>
/// The OS Cleaner's F5 report, ported to tests: a system utility could not dress itself.
/// <para>
/// Its nine sections drew <c>"✓"</c> and <c>"▾"</c> inside <c>Text()</c> — glyphs that do not scale
/// with icon metrics — because `EmptyState` and `IconButton` required the CURATED enum, and the
/// curated enum is the design system's own vocabulary (25 glyphs, spec A10), not an icon library.
/// The packs were always there (Material Symbols alone publishes 16,284 glyphs); those two
/// components were the door that was shut.
/// </para>
/// </summary>
public class IconSourceTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>A pack's glyph — the shape `MaterialSymbolsIcons.PowerRounded` hands out.</summary>
    private static readonly IconGlyph PackGlyph =
        new("power_rounded", "M12 2 L14 8 H10 Z", IconGlyphStyle.Fill);

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IReadOnlyList<HtmlNode> Rendered(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render()).ToList();

    [Fact]
    public void EmptyState_TakesAPackGlyph_AndTheCuratedSet_ThroughTheSameParameter()
    {
        // Both factories already existed and both make an Icon; what changed is that the component
        // now takes the NODE, so neither source is privileged.
        var fromPack = Rendered(EmptyState(Glyph(PackGlyph), "Nothing installed"));
        var fromCurated = Rendered(EmptyState(Icon(Icons.Info), "Nothing installed"));

        fromPack.Any(n => n.Attributes.TryGetValue("d", out var d) && d == PackGlyph.Path)
            .Should().BeTrue("the pack's own path data reaches the SVG");
        fromCurated.Any(n => n.Attributes.ContainsKey("d"))
            .Should().BeTrue("and the curated set still draws, through the same door");
    }

    [Fact]
    public void IconButton_TakesAPackGlyph_Too()
    {
        var rendered = Rendered(IconButton(Glyph(PackGlyph), "Disable at login"));

        rendered.Any(n => n.Attributes.TryGetValue("d", out var d) && d == PackGlyph.Path)
            .Should().BeTrue();
        rendered.Any(n => n.Attributes.TryGetValue("aria-label", out var l) && l == "Disable at login")
            .Should().BeTrue();
    }

    [Fact]
    public void TheComponentImposesItsOwnSize_NotTheCallersIcon()
    {
        // The caller says WHICH glyph; the component says how big it is in its own well, so an icon
        // built at 16 for a list row does not shrink an EmptyState's 32dp illustration.
        var rendered = Rendered(EmptyState(Icon(Icons.Info, IconSize.Sm), "Nothing here"));

        // The size rides the inline style on the <svg>, which is where the realizer puts it.
        rendered.Any(n => n.Tag == "svg" && n.Attributes.TryGetValue("style", out var style)
                && style.Contains("width: 32px"))
            .Should().BeTrue("the well is 32dp whatever size the caller's Icon was built at");
    }
}

/// <summary>The rest of the same report: three controls a settings screen could not finish
/// declaratively — the switch that names itself, the check that goes inert, the segmented picker
/// that had no factory at all.</summary>
public class SettingsControlTailTests
{
    [Fact]
    public void SwitchStatesItsNameAndItsDisabledBit()
    {
        var node = Switch(on: true, label: "Launch at login", disabled: true);

        node.Label.Should().Be("Launch at login");
        node.Disabled.Should().BeTrue();
    }

    [Fact]
    public void CheckboxGoesInert()
    {
        Checkbox(true, label: "Include system files", disabled: true).Disabled.Should().BeTrue();
    }

    [Fact]
    public void SegmentedControlHasAFactory_AndReachesStretch()
    {
        var node = SegmentedControl(["Apps", "Login items"], 1, _ => { }, stretch: false);

        node.SelectedIndex.Should().Be(1);
        node.Stretch.Should().BeFalse("a tab strip sizes to its content; a settings row fills");
        SegmentedControl(["A", "B"], 0).Stretch.Should().BeTrue("the default is what it always was");
    }
}

/// <summary>
/// W3 on the WEB: the same canvas the Photon engine paints becomes inline SVG, so a visualization
/// authored once runs on both targets. SVG rather than &lt;canvas&gt; because it SSRs to the same
/// pixels it hydrates to, which a blank rectangle filled by script could never do.
/// </summary>
public class CanvasWebLoweringTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Ink = new(new Color(10, 20, 30, 255));

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IReadOnlyList<HtmlNode> Render(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render()).ToList();

    [Fact]
    public void TheShapesBecomeSvgChildren_InPaintOrder()
    {
        var rendered = Render(new Canvas(p =>
        {
            p.FillCircle(20, 20, 10, Ink);
            p.FillRect(0, 0, 5, 5, Ink);
        }, width: SizeValue.Fixed(40), height: SizeValue.Fixed(40)));

        var svg = rendered.First(n => n.Tag == "svg");
        // The params overload would swallow a `because` string as a third expected item.
        svg.Children.Select(c => c.Tag).Should().Equal(["circle", "rect"]);
        svg.Attributes["viewBox"].Should().Be("0 0 40 40");
    }

    [Fact]
    public void ColoursFollowTheTheme_RatherThanBeingResolvedToOneMode()
    {
        // The web has a cascade to defer to, so a token crosses as light-dark(...) — the same
        // reason every other colour on this target is not resolved by the realizer.
        var twoTone = new ColorToken(new Color(1, 2, 3, 255), new Color(4, 5, 6, 255));
        var circle = Render(new Canvas(p => p.FillCircle(5, 5, 5, twoTone),
            SizeValue.Fixed(20), SizeValue.Fixed(20))).First(n => n.Tag == "circle");

        circle.Attributes["fill"].Should().StartWith("light-dark(");
    }

    [Fact]
    public void AnAnnularSector_BecomesAnArcPath()
    {
        // The one engine shape SVG has no primitive for: two arcs and two radial lines.
        var path = Render(new Canvas(p => p.FillAnnularSector(50, 50, 20, 40, 0, MathF.PI / 2, Ink),
            SizeValue.Fixed(100), SizeValue.Fixed(100))).First(n => n.Tag == "path");

        path.Attributes["d"].Should().Contain("A 40 40").And.Contain("A 20 20");
    }

    [Fact]
    public void AFullRingIsDrawnAsTwoHalves()
    {
        // An arc whose start and end coincide draws nothing in SVG — a full ring must be split.
        var paths = Render(new Canvas(p => p.FillAnnularSector(50, 50, 20, 40, 0, MathF.Tau, Ink),
            SizeValue.Fixed(100), SizeValue.Fixed(100))).Where(n => n.Tag == "path").ToList();

        paths.Should().HaveCount(2, "a full ring cannot be one arc");
    }

    [Fact]
    public void ADecorativeCanvasIsHidden_AndALabelledOneIsAnImage()
    {
        Render(new Canvas(_ => { })).First(n => n.Tag == "svg")
            .Attributes.Should().ContainKey("aria-hidden");

        var labelled = Render(new Canvas(_ => { }) { Label = "Disk usage by folder" })
            .First(n => n.Tag == "svg");
        labelled.Attributes["role"].Should().Be("img");
        labelled.Attributes["aria-label"].Should().Be("Disk usage by folder");
    }
}

/// <summary>
/// The canvas behaves the same on both targets where it MATTERS, and degenerate input is where a
/// divergence hides best. Photon's own guards are the reference: these pin the web to them.
/// </summary>
public class CanvasCrossTargetTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Ink = new(new Color(10, 20, 30, 255));

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IReadOnlyList<HtmlNode> Render(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render()).ToList();

    /// <summary>Shapes a canvas emits AT SSR — which needs a fixed size, because only then is the
    /// box knowable before the browser lays anything out.</summary>
    private static int Shapes(Action<ICanvasPainter> draw) =>
        Render(new Canvas(draw, SizeValue.Fixed(100), SizeValue.Fixed(100)))
            .Count(n => n.Tag is "path" or "rect" or "circle" or "line");

    [Theory]
    // Exactly the engine's early returns (DisplayList.FillAnnularSector), which used to draw an
    // inside-out path here while Photon drew nothing at all.
    [InlineData(0f, 0f, 0f, 1f)]             // no outer radius (inner 0 with a real outer is a PIE
                                             // slice and must still draw — the first draft of this
                                             // row said 10 and caught the test, not the code)
    [InlineData(5f, 20f, 1f, 1f)]            // end at the start
    [InlineData(5f, 20f, 1f, 0.5f)]          // end BEFORE the start: a mistake, not a wish
    [InlineData(20f, 20f, 0f, 1f)]           // zero-width band
    [InlineData(30f, 20f, 0f, 1f)]           // inner past outer
    public void DegenerateSectorsDrawNothing_AsOnPhoton(float inner, float outer, float start, float end)
    {
        Shapes(p => p.FillAnnularSector(50, 50, inner, outer, start, end, Ink)).Should().Be(0);
    }

    [Fact]
    public void AZeroInnerRadiusIsAPieSlice_AndDraws()
    {
        // The neighbour of the degenerate rows above, kept beside them on purpose: inner 0 is the
        // sector reaching the centre, which both targets draw.
        Shapes(p => p.FillAnnularSector(50, 50, 0, 20, 0, 1, Ink)).Should().Be(1);
    }

    [Fact]
    public void ASweepPastAFullTurn_StopsAtOne()
    {
        // Clamped rather than refused, exactly as the engine clamps it — and a full ring is two
        // halves here because an arc whose ends coincide draws nothing in SVG.
        Shapes(p => p.FillAnnularSector(50, 50, 10, 20, 0, MathF.Tau * 3, Ink)).Should().Be(2);
    }

    [Fact]
    public void AFullRingIsNotSmoothed_SoNoSeamAppears()
    {
        // Photon draws a full ring as ONE sector whose angular edges coincide, so its rounding has
        // nothing to round. Forwarding the smoothing to the two SVG halves would stroke four
        // corners and draw a seam at 0 and π that exists on this target only.
        var halves = Render(new Canvas(p => p.FillAnnularSector(50, 50, 10, 20, 0, MathF.Tau, Ink, 4),
                SizeValue.Fixed(100), SizeValue.Fixed(100)))
            .Where(n => n.Tag == "path").ToList();

        halves.Should().HaveCount(2);
        halves.Should().OnlyContain(n => !n.Attributes.ContainsKey("stroke"));
    }

    [Fact]
    public void AFillingCanvasIsMarkedForMeasurement_RatherThanDrawnAtZero()
    {
        // CSS decides a filling canvas's box AFTER this markup exists, so the server emits the
        // shell and the runtime draws once it has measured (canvas-surface.ts). Drawing here would
        // put every `Width / 2` in the top-left corner and leave it there — the divergence from
        // Photon, which lays out and paints every frame and always knows the real box.
        var svg = Render(new Canvas(p => p.FillCircle(p.Width / 2, p.Height / 2, 10, Ink)))
            .First(n => n.Tag == "svg");

        svg.Children.Should().BeEmpty("nothing is drawn until the box is known");
        svg.Attributes.Should().NotContainKey("viewBox", "there is no box to describe yet");
        // And NO marker from the server: the runtime looks a declaration up by a client-side path
        // this realizer does not have, and a placeholder would stick — the reconciler adds a
        // missing data attribute on hydration but does not overwrite one already there, so the
        // canvas would never paint. The client marks it, as InView does for the same reason.
        svg.Attributes.Should().NotContainKey("data-eq-canvas-fill");
    }

    [Fact]
    public void AFixedSizeCanvasIsDrawnByTheServer()
    {
        var svg = Render(new Canvas(p => p.FillCircle(p.Width / 2, p.Height / 2, 10, Ink),
                SizeValue.Fixed(80), SizeValue.Fixed(40)))
            .First(n => n.Tag == "svg");

        svg.Attributes["viewBox"].Should().Be("0 0 80 40");
        svg.Children.Should().ContainSingle();
        svg.Children[0].Attributes["cx"].Should().Be("40", "the painter answered with the real box");
    }

    [Fact]
    public void ADecorativeCanvasDoesNotSwallowThePressUnderIt()
    {
        // Photon registers NO region for a handler-less canvas, so the press reaches what is under
        // it. The DOM has no such rule — a filling svg over a Stack eats every click — so the
        // decorative case says so in CSS.
        var decorative = Render(new Canvas(_ => { })).First(n => n.Tag == "svg");
        decorative.Attributes["style"].Should().Contain("pointer-events");

        var interactive = Render(new Canvas(_ => { }) { OnPointerDown = _ => { } }).First(n => n.Tag == "svg");
        interactive.Attributes.TryGetValue("style", out var style);
        (style ?? "").Should().NotContain("pointer-events", "a canvas that listens must receive");
    }
}
