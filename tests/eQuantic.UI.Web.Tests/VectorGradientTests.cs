using System.Runtime.CompilerServices;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Gradients in artwork. Until this, a shape whose fill named a paint server was dropped: a logo
/// drawn as a run of two colours arrived as nothing at all, which is the one failure mode a
/// designer notices immediately and a test never did.
/// </summary>
public class VectorGradientTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>
    /// The same artwork twice, on one page. Each drawing used to carry its own <c>&lt;defs&gt;</c>,
    /// so the document held N identical definitions of one id — and <c>url(#id)</c> binds to the
    /// FIRST in document order. With an AdaptiveNode putting every arm in the page, that first one
    /// is inside the arm the media query hides, and a paint server that is not rendered paints
    /// nothing: the shape came out unfilled on the layout it was written for.
    /// </summary>
    [Fact]
    public void TheSameRunTwice_IsDeclaredOnceForTheDocument()
    {
        var sink = new GradientSink();
        GradientSink.Ambient = sink;
        try
        {
            var page = new Column(gap: 0);
            page.Add(new Drawing(Sky(), 100, 100));
            page.Add(new Drawing(Sky(), 100, 100));
            var rendered = Render(page);

            Walk(rendered).Where(node => node.Tag == "defs").Should().BeEmpty(
                "a drawing no longer carries the document's paint servers");

            var container = WebRealizer.Lower(new Box(), Theme) is not null
                ? sink.Container().Render()
                : throw new InvalidOperationException();

            var declared = Walk(container)
                .Where(node => node.Tag is "linearGradient" or "radialGradient")
                .Select(node => node.Attributes["id"])
                .ToList();

            declared.Should().OnlyHaveUniqueItems("the id IS the content, so a repeat is the same def");
            declared.Should().HaveCount(2, "the artwork has two runs, and it was drawn twice");
        }
        finally
        {
            GradientSink.Ambient = null;
        }
    }

    /// <summary>
    /// The container has to be RENDERED to be usable. `display:none` is what broke this in the
    /// first place — a paint server inside one is not rendered, and referencing it paints nothing —
    /// so the container is zero-sized and out of flow instead, and aria-hidden keeps it out of the
    /// accessibility tree without taking it out of the render tree.
    /// </summary>
    [Fact]
    public void TheContainer_IsOutOfFlowRatherThanHidden()
    {
        var sink = new GradientSink();
        sink.Add("eq-g-test", new VectorPaint(VectorPaintKind.LinearGradient, Primitives.Color.Black)
        {
            Gradient = new VectorGradient(0, 0, 0, 1, 0, false,
                [new VectorStop(0, Primitives.Color.Black), new VectorStop(1, Primitives.Color.White)]),
        });

        var container = sink.Container().Render();

        container.Attributes["id"].Should().Be("eq-vectors");
        container.Attributes["aria-hidden"].Should().Be("true");
        var style = container.Attributes.TryGetValue("style", out var value) ? value : string.Empty;
        style.Replace(" ", string.Empty).Should().Contain("position:absolute")
            .And.NotContain("display:none",
            "a paint server that is not rendered paints nothing — that is the bug, not the fix");
    }

    /// <summary>Defs AFTER the shapes on purpose: an exporter writes them in either order, and a
    /// single forward pass would have answered "unknown" for half the files in the world.</summary>
    private static VectorDrawing Sky() => SvgDocument.Parse("""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
          <rect width="100" height="100" fill="url(#sky)"/>
          <circle cx="30" cy="30" r="20" fill="url(#sun)"/>
          <defs>
            <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stop-color="#4facfe"/>
              <stop offset="1" stop-color="#00f2fe" stop-opacity="0.8"/>
            </linearGradient>
            <radialGradient id="sun" cx="30%" cy="30%" r="40%">
              <stop offset="0" stop-color="#ffffff"/>
              <stop offset="1" stop-color="#ffcc00"/>
            </radialGradient>
          </defs>
        </svg>
        """);

    private const string TwoShapesOneRun =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\">"
        + "<linearGradient id=\"run\">"
        + "<stop offset=\"0\" stop-color=\"#000000\"/><stop offset=\"1\" stop-color=\"#ffffff\"/>"
        + "</linearGradient>"
        + "<rect width=\"4\" height=\"10\" fill=\"url(#run)\"/>"
        + "<rect x=\"6\" width=\"4\" height=\"10\" fill=\"url(#run)\"/>"
        + "</svg>";

    [Fact]
    public void ALinearGradientArrivesWithItsAxisAndItsStops()
    {
        var fill = Sky().Shapes[0].Fill;

        fill.Kind.Should().Be(VectorPaintKind.LinearGradient);
        fill.IsGradient.Should().BeTrue();
        var gradient = fill.Gradient!.Value;
        (gradient.X1, gradient.Y1, gradient.X2, gradient.Y2).Should().Be((0f, 0f, 0f, 1f));
        gradient.UserSpace.Should().BeFalse("the SVG default is a fraction of the shape's own box");
        gradient.Stops.Should().HaveCount(2);
        gradient.Stops[0].Color.Should().Be(Color.FromRgb(0x4f, 0xac, 0xfe));
        gradient.Stops[1].Offset.Should().Be(1);
        gradient.Stops[1].Color.A.Should().Be(204, "stop-opacity folds into the stop's own alpha");
    }

    /// <summary>A percentage and a number say the same thing in the default units, so both spellings
    /// collapse to the fraction they are.</summary>
    [Fact]
    public void ARadialGradientReadsItsCentreAndRadius_InEitherSpelling()
    {
        var fill = Sky().Shapes[1].Fill;

        fill.Kind.Should().Be(VectorPaintKind.RadialGradient);
        var gradient = fill.Gradient!.Value;
        gradient.X1.Should().BeApproximately(0.3f, 0.001f);
        gradient.Y1.Should().BeApproximately(0.3f, 0.001f);
        gradient.Radius.Should().BeApproximately(0.4f, 0.001f);
    }

    /// <summary>`href` is what an exporter writes when two gradients share a palette: the borrower
    /// keeps its own geometry and takes the stops.</summary>
    [Fact]
    public void AGradientBorrowsTheStopsItPointsAt()
    {
        var drawing = SvgDocument.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
              <linearGradient id="base" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" stop-color="#ff0000"/>
                <stop offset="1" stop-color="#0000ff"/>
              </linearGradient>
              <linearGradient id="sideways" xlink:href="#base" x1="0" y1="0" x2="1" y2="0"/>
              <rect width="10" height="10" fill="url(#sideways)"/>
            </svg>
            """);

        var gradient = drawing.Shapes[0].Fill.Gradient!.Value;
        gradient.X2.Should().Be(1, "the borrower keeps its own axis");
        gradient.Stops.Should().HaveCount(2);
        gradient.Stops[0].Color.Should().Be(Color.FromRgb(255, 0, 0));
    }

    /// <summary>
    /// A paint server this reader does not understand — a pattern, or a gradient with no stops —
    /// still answers "no paint", which is what every paint server answered before. The shape draws
    /// nothing rather than something nobody chose.
    /// </summary>
    [Fact]
    public void AnUnknownPaintServerStillPaintsNothing()
    {
        var drawing = SvgDocument.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
              <rect width="10" height="10" fill="url(#nowhere)"/>
              <rect width="10" height="10" fill="#00ff00"/>
            </svg>
            """);

        drawing.Shapes.Should().ContainSingle("an unpainted shape is dropped, as it always was");
        drawing.Shapes[0].Fill.Kind.Should().Be(VectorPaintKind.Solid);
    }

    /// <summary>
    /// On the web the run stays a run: a <c>defs</c> with the paint server, and a fill that NAMES
    /// it. Flattening it to a colour here would have been one line and the wrong answer — the
    /// browser interpolates in the space it chooses, at whatever size the box ends up.
    /// </summary>
    [Fact]
    public void TheWebDeclaresTheRunAndNamesIt()
    {
        var html = Render(new Drawing(Sky(), 200));

        var defs = Walk(html).Single(node => node.Tag == "defs");
        var linear = Walk(defs).Single(node => node.Tag == "linearGradient");
        var radial = Walk(defs).Single(node => node.Tag == "radialGradient");

        linear.Attributes["y2"].Should().Be("1");
        linear.Attributes.Should().NotContainKey("gradientUnits", "objectBoundingBox is SVG's own default");
        var stops = Walk(linear).Where(node => node.Tag == "stop").ToList();
        stops.Should().HaveCount(2);
        stops[0].Attributes["stop-color"].Should().Be("#4facfe");
        stops[1].Attributes["stop-opacity"].Should().Be("0.8",
            "the alpha rides stop-opacity, the spelling every renderer agrees on");

        radial.Attributes["r"].Should().Be("0.4");

        var paths = Walk(html).Where(node => node.Tag == "path").ToList();
        paths[0].Attributes["fill"].Should().Be($"url(#{linear.Attributes["id"]})");
        paths[1].Attributes["fill"].Should().Be($"url(#{radial.Attributes["id"]})");
    }

    /// <summary>The id is the CONTENT, so one run named twice is declared once — and the same run
    /// in another drawing lands on the same id rather than a counter nobody can reproduce.</summary>
    [Fact]
    public void OneRunIsDeclaredOnceHoweverManyShapesNameIt()
    {
        var twice = SvgDocument.Parse(TwoShapesOneRun);

        var html = Render(new Drawing(twice, 100));

        Walk(html).Count(node => node.Tag == "linearGradient").Should().Be(1);
        var paths = Walk(html).Where(node => node.Tag == "path").ToList();
        paths[0].Attributes["fill"].Should().Be(paths[1].Attributes["fill"]);
    }

    /// <summary>A gradient can paint the OUTLINE too — the stroke goes through the same resolution
    /// as the fill, so a ring drawn as a run of colours survives.</summary>
    [Fact]
    public void AStrokeTakesAGradientToo()
    {
        var drawing = SvgDocument.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
              <linearGradient id="ring">
                <stop offset="0" stop-color="#111111"/>
                <stop offset="1" stop-color="#eeeeee"/>
              </linearGradient>
              <path d="M0 5h10" stroke="url(#ring)" stroke-width="2" fill="none"/>
            </svg>
            """);

        var shape = drawing.Shapes[0];
        shape.Fill.Paints.Should().BeFalse();
        shape.Stroke.Kind.Should().Be(VectorPaintKind.LinearGradient);
        shape.Stroke.Gradient!.Value.Stops.Should().HaveCount(2);
    }

    /// <summary>
    /// The id a gradient gets is a HASH of the run, and both producers compute it — the server from
    /// C# and the client twin from TypeScript. A byte of disagreement means the client repaints
    /// every path that names one, silently, on the first hydration. This writes the pairs the
    /// TypeScript spec then recomputes (regenerate with <c>EQ_UPDATE_GRADIENT_FIXTURE=1</c>).
    /// </summary>
    [Fact]
    public void TheIdIsTheSameOnBothProducers()
    {
        var runs = new (string Label, VectorGradient Run)[]
        {
            ("linear-two", new VectorGradient(0, 0, 0, 1, 0, false,
                [new VectorStop(0, Color.FromRgb(0x4f, 0xac, 0xfe)),
                 new VectorStop(1, Color.FromRgba(0x00, 0xf2, 0xfe, 204))])),
            ("radial-centre", new VectorGradient(0.3f, 0.3f, 0, 0, 0.4f, false,
                [new VectorStop(0, Color.White), new VectorStop(1, Color.FromRgb(255, 204, 0))])),
            ("user-space-three", new VectorGradient(4, 8, 96, 8, 0, true,
                [new VectorStop(0, Color.Black),
                 new VectorStop(0.5f, Color.FromRgb(128, 128, 128)),
                 new VectorStop(1, Color.White)])),
        };

        var lines = runs.Select(entry => $"{Serialize(entry.Run)}|{WebRealizer.GradientId(entry.Run)}");
        var text = string.Join("\n", lines) + "\n";
        var path = FixturePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_GRADIENT_FIXTURE") == "1")
        {
            File.WriteAllText(path, text);
            return;
        }

        File.Exists(path).Should().BeTrue($"the twin reads its cases from {path}");
        File.ReadAllText(path).Should().Be(text,
            "the ids the two producers compute are the same or hydration repaints every gradient "
            + "(EQ_UPDATE_GRADIENT_FIXTURE=1 to refresh)");
    }

    /// <summary>
    /// The case, written the way BOTH sides read it. Invariant on purpose: a fixture that spells
    /// 0.3 as "0,3" on one machine is a fixture that fails on the next one, and this file is read
    /// by a TypeScript spec that has never heard of a decimal comma.
    /// </summary>
    private static string Serialize(VectorGradient run)
    {
        static string N(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var stops = string.Join(",", run.Stops.Select(stop =>
            $"{N(stop.Offset)}:{stop.Color.R:x2}{stop.Color.G:x2}{stop.Color.B:x2}{stop.Color.A:x2}"));
        return $"{(run.UserSpace ? "u" : "o")}|{N(run.X1)}|{N(run.Y1)}|{N(run.X2)}|{N(run.Y2)}|{N(run.Radius)}|{stops}";
    }

    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "__fixtures__",
            "vector-gradient.txt");
    }
}
