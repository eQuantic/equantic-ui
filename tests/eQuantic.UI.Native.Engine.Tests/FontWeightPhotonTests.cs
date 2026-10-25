using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Text is set in each of the nine weights on Photon (#1082). CoreText is asked for the weight itself, its
/// weight trait, where it was asked for one bold trait at 600 and above: so the system face drew two weights
/// and a family that ships a file per weight was drawn from its Regular or its Bold file whatever the
/// weight asked. The family here is the tests' own (TestFont), a file at 300, 400 and 700 whose glyphs are
/// 0.7, 1 and 1.3 of an em wide, so the width a text measures says which file set it.
/// </summary>
public class FontWeightPhotonTests
{
    private const string Assembly = "eQuantic.UI.Native.Engine.Tests";

    private static readonly FontFamily WeightFace = new("eQuantic Weight Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-300.ttf", 300, 300),
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-400.ttf", 400, 400),
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-700.ttf", 700, 700));

    private static readonly TypeStyle Body = new(17, 22, FontWeight.Regular, 0, 1.4f);

    public FontWeightPhotonTests() => FaceResolution.Clear();

    [MacTheory]
    [InlineData(FontWeight.Light, 0.7f)]
    [InlineData(FontWeight.Regular, 1f)]
    [InlineData(FontWeight.Bold, 1.3f)]
    [InlineData(FontWeight.Thin, 0.7f)]      // no file sets it: the one a browser would choose
    [InlineData(FontWeight.Medium, 1f)]
    [InlineData(FontWeight.SemiBold, 1.3f)]
    [InlineData(FontWeight.Black, 1.3f)]
    public void AFamilyThatShipsAFilePerWeight_IsSetInTheFileOfTheWeightAsked(FontWeight weight, float em)
    {
        var measured = new CoreTextService().Measure("Handgloves", Body with { Family = WeightFace, Weight = weight }, 1f,
            float.PositiveInfinity, 1);

        measured.Width.Should().BeApproximately(10 * 17 * em, 0.5f);
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    [MacFact]
    public void TheSystemFace_DrawsTheNineWeightsNineWays()
    {
        var service = new CoreTextService();

        var pictures = Enum.GetValues<FontWeight>()
            .Select(weight => Convert.ToBase64String(service.Rasterize("Handgloves", Body with { Weight = weight }, 1f,
                float.PositiveInfinity, 1, 2f, TextAlignment.Start)!.Alpha))
            .ToList();

        pictures.Should().HaveCount(9).And.OnlyHaveUniqueItems();
    }

    /// <summary>The weight and the slant are asked for together: a light italic is neither the light
    /// upright nor the regular italic.</summary>
    [MacFact]
    public void TheWeightAndTheSlant_AreAskedForTogether()
    {
        var service = new CoreTextService();
        byte[] Coverage(TypeStyle style) =>
            service.Rasterize("Handgloves", style, 1f, float.PositiveInfinity, 1, 2f, TextAlignment.Start)!.Alpha;

        var lightItalic = Coverage(Body with { Weight = FontWeight.Light, Italic = true });

        lightItalic.Should().NotEqual(Coverage(Body with { Weight = FontWeight.Light }), "a light italic is italic");
        lightItalic.Should().NotEqual(Coverage(Body with { Italic = true }), "a light italic is light");
    }

    /// <summary>CoreText's scale is read off a weight's number: Apple's own values at the nine, and every
    /// weight heavier than the one before it, so none falls to a default.</summary>
    [Fact]
    public void EveryWeight_IsItsOwnPointOnCoreTextsScale()
    {
        CoreTextWeights.Scale((int)FontWeight.Thin).Should().Be(-0.8);
        CoreTextWeights.Scale((int)FontWeight.Light).Should().Be(-0.4);
        CoreTextWeights.Scale((int)FontWeight.Regular).Should().Be(0);
        CoreTextWeights.Scale((int)FontWeight.Black).Should().Be(0.62);

        Enum.GetValues<FontWeight>().OrderBy(weight => (int)weight).Select(weight => CoreTextWeights.Scale((int)weight))
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }
}
