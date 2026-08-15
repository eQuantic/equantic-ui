using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Reading a `.svg` FILE into target-neutral data. This is the seam where artwork stops being a
/// file and becomes something both realizers can draw — so what is pinned here is the subset's
/// promise: every shape reduces to path data, a group's transform is BAKED in rather than carried,
/// and what the subset cannot express is DROPPED rather than half-drawn.
/// </summary>
public class SvgDocumentTests
{
    private static VectorDrawing Parse(string svg) => SvgDocument.Parse(svg);

    [Fact]
    public void TheViewBoxIsTheDrawingsOwnGrid()
    {
        var drawing = Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40">
              <path d="M0 0h120v40H0z" fill="#4F46E5"/>
            </svg>
            """);

        drawing.Width.Should().Be(120);
        drawing.Height.Should().Be(40);
        drawing.Aspect.Should().Be(3);
        drawing.ViewBox.Should().Be("0 0 120 40");
        drawing.Shapes.Should().ContainSingle();
        drawing.Shapes[0].Fill.Should().Be(VectorPaint.Solid(Color.FromRgb(0x4F, 0x46, 0xE5)));
    }

    /// <summary>Without a viewBox the authored width and height are the grid — a file with neither
    /// cannot be placed at all, and an empty drawing is the visible, survivable answer.</summary>
    [Fact]
    public void WidthAndHeightStandInForAMissingViewBox()
    {
        Parse("""<svg width="64px" height="32px"><rect width="64" height="32"/></svg>""")
            .Aspect.Should().Be(2);

        Parse("""<svg><rect width="10" height="10"/></svg>""").IsEmpty.Should().BeTrue();
        Parse("not markup at all").Should().Be(VectorDrawing.Empty);
    }

    /// <summary>Every shape is path data downstream: one geometry vocabulary instead of six.</summary>
    [Theory]
    [InlineData("""<rect x="1" y="2" width="8" height="6"/>""", "M1 2H9V8H1Z")]
    [InlineData("""<line x1="0" y1="0" x2="4" y2="4" stroke="black"/>""", "M0 0L4 4")]
    [InlineData("""<polygon points="0,0 4,0 4,4"/>""", "M0 0L4 0L4 4Z")]
    [InlineData("""<polyline points="0,0 4,0 4,4" stroke="black" fill="none"/>""", "M0 0L4 0L4 4")]
    public void EveryShapeReducesToPathData(string element, string expected)
    {
        var drawing = Parse($"""<svg viewBox="0 0 10 10">{element}</svg>""");

        drawing.Shapes.Should().ContainSingle();
        drawing.Shapes[0].Path.Should().Be(expected);
    }

    /// <summary>A circle needs TWO arcs: one arc whose endpoints coincide is undefined, and a
    /// single-arc circle renders as nothing at all.</summary>
    [Fact]
    public void ACircleIsTwoHalfArcs()
    {
        var drawing = Parse("""<svg viewBox="0 0 10 10"><circle cx="5" cy="5" r="4"/></svg>""");

        drawing.Shapes[0].Path.Should().Be("M1 5A4 4 0 1 0 9 5A4 4 0 1 0 1 5Z");
    }

    [Fact]
    public void ARoundedRectangleKeepsItsCorners()
    {
        var drawing = Parse("""<svg viewBox="0 0 10 10"><rect width="10" height="10" rx="2"/></svg>""");

        // Both radii from one attribute (the spec's rule, and what exporters emit).
        drawing.Shapes[0].Path.Should().StartWith("M2 0H8A2 2 0 0 1 10 2");
    }

    /// <summary>
    /// The heart of the reader: a group's transform is FLATTENED into the data it wraps. Carrying
    /// it would mean the two targets disagree — the web has a transform attribute and the native
    /// rasterizer takes a glyph and a box.
    /// </summary>
    [Fact]
    public void AGroupsTransformIsBakedIntoThePathsItWraps()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 100 100">
              <g transform="translate(10 20) scale(2)">
                <path d="M0 0h5v5"/>
              </g>
            </svg>
            """);

        drawing.Shapes[0].Path.Should().Be("M10 20 L20 20 L20 30");
    }

    /// <summary>Nesting composes outward, and the pen scales with the shape: a scaled figure
    /// wearing an unscaled outline is the tell of a transform half-applied.</summary>
    [Fact]
    public void NestedGroupsComposeAndTheStrokeScalesWithThem()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 100 100">
              <g transform="scale(2)">
                <g transform="translate(1 1)">
                  <path d="M0 0h1" stroke="black" stroke-width="1.5" fill="none"/>
                </g>
              </g>
            </svg>
            """);

        drawing.Shapes[0].Path.Should().Be("M2 2 L4 2");
        drawing.Shapes[0].StrokeWidth.Should().Be(3);
    }

    /// <summary>Painting is inherited, and `style` wins over the presentation attribute — the
    /// cascade's answer, which every exporter relies on when it writes both.</summary>
    [Fact]
    public void PaintIsInheritedAndStyleWins()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 10 10">
              <g fill="#ff0000">
                <path d="M0 0h1"/>
                <path d="M0 1h1" fill="#00ff00" style="fill:#0000ff"/>
              </g>
            </svg>
            """);

        drawing.Shapes[0].Fill.Color.Should().Be(Color.FromRgb(255, 0, 0));
        drawing.Shapes[1].Fill.Color.Should().Be(Color.FromRgb(0, 0, 255));
    }

    /// <summary>
    /// `currentColor` is the artwork ASKING to be tinted, and it is what lets one logo follow a
    /// theme. It stays unresolved in the data — the realizer answers it, because only the realizer
    /// knows what the drawing is being tinted with.
    /// </summary>
    [Fact]
    public void CurrentColorStaysUnresolvedUntilSomethingTintsIt()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 10 10"><path d="M0 0h1" fill="currentColor"/></svg>
            """);

        drawing.Shapes[0].Fill.Kind.Should().Be(VectorPaintKind.Inherit);
        drawing.Shapes[0].Fill.Resolve(Color.FromRgb(9, 9, 9)).Should().Be(Color.FromRgb(9, 9, 9));
    }

    [Fact]
    public void ColorsArriveInEverySpellingArtworkUses()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 10 10">
              <path d="M0 0h1" fill="#abc"/>
              <path d="M0 1h1" fill="rgb(16, 32, 48)"/>
              <path d="M0 2h1" fill="rgba(16, 32, 48, 0.5)"/>
              <path d="M0 3h1" fill="teal"/>
              <path d="M0 4h1" fill="#11223344"/>
            </svg>
            """);

        drawing.Shapes[0].Fill.Color.Should().Be(Color.FromRgb(0xAA, 0xBB, 0xCC));
        drawing.Shapes[1].Fill.Color.Should().Be(Color.FromRgb(16, 32, 48));
        drawing.Shapes[2].Fill.Color.A.Should().Be(128);
        drawing.Shapes[3].Fill.Color.Should().Be(Color.FromRgb(0, 128, 128));
        drawing.Shapes[4].Fill.Color.Should().Be(Color.FromRgba(0x11, 0x22, 0x33, 0x44));
    }

    /// <summary>The opacities land where they belong: the element's on the shape, the paint's on
    /// the colour it applies to. Folding them together would make one un-editable.</summary>
    [Fact]
    public void TheThreeOpacitiesLandWhereTheyBelong()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 10 10">
              <path d="M0 0h1" fill="#000000" opacity="0.5" fill-opacity="0.4"/>
            </svg>
            """);

        drawing.Shapes[0].Opacity.Should().Be(0.5f);
        drawing.Shapes[0].Fill.Color.A.Should().Be(102);   // 255 × 0.4
    }

    [Fact]
    public void TheFillRuleSurvives()
    {
        Parse("""<svg viewBox="0 0 10 10"><path d="M0 0h1" fill-rule="evenodd"/></svg>""")
            .Shapes[0].EvenOdd.Should().BeTrue();
    }

    /// <summary>
    /// The fence, drawn where a reader can see it — and moved, once, when a gradient stopped being
    /// outside it. A run of colours is now understood; a font and a pattern still are not, so those
    /// shapes arrive MISSING rather than filled with something nobody chose.
    /// </summary>
    [Fact]
    public void WhatTheSubsetCannotExpressIsDroppedRatherThanHalfDrawn()
    {
        var drawing = Parse("""
            <svg viewBox="0 0 10 10">
              <defs>
                <linearGradient id="g"><stop offset="0" stop-color="#ffffff"/><stop offset="1" stop-color="#000000"/></linearGradient>
                <pattern id="p"><rect width="1" height="1" fill="#ff0000"/></pattern>
              </defs>
              <path d="M0 0h1" fill="url(#g)"/>
              <path d="M0 2h1" fill="url(#p)"/>
              <text x="0" y="9">not a drawing</text>
              <path d="M0 1h1" fill="#123456"/>
            </svg>
            """);

        drawing.Shapes.Should().HaveCount(2,
            "the gradient is understood; the patterned shape and the text are not drawings this model can carry");
        drawing.Shapes[0].Fill.Kind.Should().Be(VectorPaintKind.LinearGradient);
        drawing.Shapes[1].Fill.Color.Should().Be(Color.FromRgb(0x12, 0x34, 0x56));
    }

    /// <summary>Comments, declarations, namespace prefixes and entities are what a real exported
    /// file is FULL of — none of them is a shape, and none of them may derail the read.</summary>
    [Fact]
    public void ARealExportersPreambleIsWalkedPast()
    {
        var drawing = Parse("""
            <?xml version="1.0" encoding="UTF-8"?>
            <!-- Generator: some exporter -->
            <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd">
            <svg:svg xmlns:svg="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
              <svg:title>Logo &amp; mark</svg:title>
              <svg:path d="M2 2h20v20H2z" svg:fill="#000"/>
            </svg:svg>
            """);

        drawing.Width.Should().Be(24);
        drawing.Shapes.Should().ContainSingle();
    }

    /// <summary>An invisible shape is usually a hit-test rectangle an exporter left behind. It is
    /// dropped, which keeps the raster count on the native side honest.</summary>
    [Fact]
    public void AShapeWithNoPaintAtAllIsNotAShape()
    {
        Parse("""<svg viewBox="0 0 10 10"><rect width="10" height="10" fill="none"/></svg>""")
            .Shapes.Should().BeEmpty();
    }
}
