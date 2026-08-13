using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// THE SLANT IS A CUT OF THE FAMILY, not a shear applied afterwards — which is why it lives on the
/// <see cref="TypeStyle"/> the measurer, the rasterizer and the raster cache all key on. A slant
/// that lived anywhere else would be a paragraph measured upright and drawn leaning.
/// <para>
/// The assertions compare PIXELS rather than widths on purpose: an italic cut of a system face can
/// carry the same advances as its upright twin, so a width test would pass on a build that quietly
/// dropped the trait. Coverage bytes cannot: the same string in two cuts is two different pictures.
/// </para>
/// </summary>
public class ItalicFaceTests
{
    private static readonly CoreTextService Service = new();

    private static readonly TypeStyle Upright = new(15, 20, FontWeight.Regular, 0f, 1.3f);

    private static byte[] Coverage(TypeStyle style) =>
        Service.Rasterize("Handgloves", style, 1f, float.PositiveInfinity, 1, 2f)!.Alpha;

    [MacFact]
    public void TheSlantChangesTheGlyphsThatAreDrawn()
    {
        Coverage(Upright with { Italic = true }).Should().NotEqual(Coverage(Upright),
            "asking for italic must reach the face, not stop at the style object");
    }

    /// <summary>
    /// Bold and italic are one call to the platform, and this is the test that says why: asked in
    /// SEQUENCE, the second copy is made from the original descriptor and drops the first trait, so
    /// bold italic comes out merely italic — and nothing else in the suite would have noticed.
    /// </summary>
    [MacFact]
    public void BoldAndItalicCompose()
    {
        var boldItalic = Upright with { Weight = FontWeight.Bold, Italic = true };

        Coverage(boldItalic).Should().NotEqual(Coverage(Upright with { Italic = true }),
            "bold italic is not italic");
        Coverage(boldItalic).Should().NotEqual(Coverage(Upright with { Weight = FontWeight.Bold }),
            "bold italic is not bold");
    }

    /// <summary>
    /// The FAMILY survives the trait: italic code is still code, so every glyph keeps one advance.
    /// A trait asked for on the wrong descriptor is how a slanted code block silently stops being
    /// a column.
    /// </summary>
    [MacFact]
    public void ItalicCodeIsStillMonospaced()
    {
        var italicMono = Upright with { Mono = true, Italic = true };
        float Width(string content) =>
            Service.Measure(content, italicMono, 1f, float.PositiveInfinity, 1).Width;

        Width("iiiiiiii").Should().BeApproximately(Width("MMMMMMMM"), 0.5f);
    }

    /// <summary>Two cuts are two cache entries — the style carries the axis, so neither can serve
    /// the other's pixels.</summary>
    [MacFact]
    public void TheCutTravelsOnTheStyle()
    {
        (Upright with { Italic = true }).Should().NotBe(Upright);
    }
}
