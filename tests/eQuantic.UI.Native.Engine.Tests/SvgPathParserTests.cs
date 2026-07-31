using eQuantic.UI.Native.Framework;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>W4 icons: the pure-C# SVG path parser — the ONE normalizer every platform rasterizer
/// consumes. Everything lowers to moves/lines/cubics/closes.</summary>
public class SvgPathParserTests
{
    [Fact]
    public void AbsoluteMoveLine_Close_ParsesToVerbs()
    {
        var segments = SvgPath.Parse("M4 3h5v7L4 10z");

        segments.Should().HaveCount(5);
        segments[0].Verb.Should().Be(PathVerb.Move);
        segments[1].Verb.Should().Be(PathVerb.Line);
        segments[1].End.X.Should().Be(9);   // h5 is relative-horizontal
        segments[1].End.Y.Should().Be(3);
        segments[2].End.Y.Should().Be(10);  // v7
        segments[4].Verb.Should().Be(PathVerb.Close);
    }

    [Fact]
    public void CompactNumbers_AndNegatives_Lex()
    {
        var segments = SvgPath.Parse("M1.5.5l-1-1");

        segments[0].End.X.Should().Be(1.5f);
        segments[0].End.Y.Should().Be(0.5f);
        segments[1].End.X.Should().Be(0.5f);
        segments[1].End.Y.Should().Be(-0.5f);
    }

    [Fact]
    public void Quadratic_ElevatesToCubic()
    {
        var segments = SvgPath.Parse("M0 0Q3 0 3 3");

        segments[1].Verb.Should().Be(PathVerb.Cubic);
        segments[1].C1.X.Should().BeApproximately(2f, 0.001f);   // 0 + 2/3·(3−0)
        segments[1].C2.Y.Should().BeApproximately(1f, 0.001f);   // 3 + 2/3·(0−3)
        segments[1].End.X.Should().Be(3);
    }

    [Fact]
    public void Arc_ConvertsToCubics_EndingExactly()
    {
        // Half circle radius 5: land exactly on the endpoint, split into ≤90° cubics.
        var segments = SvgPath.Parse("M0 0a5 5 0 0 1 10 0");

        segments.Should().HaveCountGreaterThan(2);
        segments[^1].Verb.Should().Be(PathVerb.Cubic);
        segments[^1].End.X.Should().BeApproximately(10, 0.001f);
        segments[^1].End.Y.Should().BeApproximately(0, 0.001f);
    }

    [Fact]
    public void CuratedGlyphs_AllParse()
    {
        foreach (Primitives.Icons glyph in Enum.GetValues<Primitives.Icons>())
        {
            var resolved = Primitives.CuratedIcons.Resolve(glyph);
            SvgPath.Parse(resolved.Path).Should().NotBeEmpty(
                $"the curated '{resolved.Name}' glyph must lower to drawable segments");
        }
    }

    [Fact]
    public void ImplicitLineAfterMove_AndMalformedTail_KeepParsed()
    {
        var segments = SvgPath.Parse("M0 0 5 5 Xnope");

        segments.Should().HaveCount(2, "M's extra pairs are implicit lines; junk stops the parse");
        segments[1].Verb.Should().Be(PathVerb.Line);
        segments[1].End.X.Should().Be(5);
    }
}
