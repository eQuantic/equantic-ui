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
/// <para>
/// The families and the widths they measure are the Windows shell's too (<see cref="FontWeightWindowsTests"/>),
/// so the two targets are held to one table: every shell sets a weight in the file
/// <see cref="ShippedFaces.FileFor"/> chooses (#1099).
/// </para>
/// </summary>
public class FontWeightPhotonTests
{
    internal const string Assembly = "eQuantic.UI.Native.Engine.Tests";

    internal static readonly FontFamily WeightFace = new("eQuantic Weight Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-300.ttf", 300, 300),
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-400.ttf", 400, 400),
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-700.ttf", 700, 700));

    /// <summary>The width, in ems, a weight sets <see cref="WeightFace"/> at: the width of the file a browser
    /// would choose for it.</summary>
    public static TheoryData<FontWeight, float> FilePerWeight => new()
    {
        { FontWeight.Light, 0.7f },
        { FontWeight.Regular, 1f },
        { FontWeight.Bold, 1.3f },
        { FontWeight.Thin, 0.7f },      // no file sets it: the one a browser would choose
        { FontWeight.Medium, 1f },
        { FontWeight.SemiBold, 1.3f },
        { FontWeight.Black, 1.3f },
    };

    /// <summary>Two files whose OS/2 says one weight, 250, and whose catalog says 100 and 200, as Google's
    /// static Thin and ExtraLight of Inter do (the catalog reads the two from their STAT, FontCatalogTests),
    /// their glyphs 0.5 and 0.6 of an em wide. An engine that reads the weight class alone sees one weight.</summary>
    internal static readonly FontFamily ThinFace = new("eQuantic Thin Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/ThinFace-100.ttf", 100, 100),
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/ThinFace-200.ttf", 200, 200));

    /// <summary>The width, in ems, a weight sets <see cref="ThinFace"/> at: two widths, one for each file.</summary>
    public static TheoryData<FontWeight, float> OneWeightClassTwoWeights => new()
    {
        { FontWeight.Thin, 0.5f },
        { FontWeight.ExtraLight, 0.6f },
    };

    /// <summary>One variable file whose weight axis runs from 100 to 900, its glyphs 0.5 of an em wide at
    /// 100, 1 at its default, 400, and 1.5 at 900, in a straight line on each side.</summary>
    private static readonly FontFamily VariableFace = new("eQuantic Variable Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/weights/WeightFace-Variable.ttf", 100, 900));

    private static readonly TypeStyle Body = new(17, 22, FontWeight.Regular, 0, 1.4f);

    public FontWeightPhotonTests() => FaceResolution.Clear();

    [MacTheory]
    [MemberData(nameof(FilePerWeight))]
    public void AFamilyThatShipsAFilePerWeight_IsSetInTheFileOfTheWeightAsked(FontWeight weight, float em)
    {
        var measured = new CoreTextService().Measure("Handgloves", Body with { Family = WeightFace, Weight = weight }, 1f,
            float.PositiveInfinity, 1);

        measured.Width.Should().BeApproximately(10 * 17 * em, 0.5f);
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    [MacTheory]
    [MemberData(nameof(OneWeightClassTwoWeights))]
    public void TwoFilesThatSayOneWeight_AreEachSetAtTheWeightTheCatalogGivesIt(FontWeight weight, float em)
    {
        var measured = new CoreTextService().Measure("Handgloves", Body with { Family = ThinFace, Weight = weight }, 1f,
            float.PositiveInfinity, 1);

        measured.Width.Should().BeApproximately(10 * 17 * em, 0.5f);
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    /// <summary>A variable file is opened at the weight on its axis: opened as it is, it set every weight at
    /// its default instance, where the bold trait laid over a matched face had reached its Bold.</summary>
    [MacTheory]
    [InlineData(FontWeight.Thin, 0.5f)]
    [InlineData(FontWeight.Light, 0.5f + 0.5f * 2 / 3)]
    [InlineData(FontWeight.Regular, 1f)]
    [InlineData(FontWeight.Medium, 1.1f)]
    [InlineData(FontWeight.Bold, 1.3f)]
    [InlineData(FontWeight.Black, 1.5f)]
    public void AVariableFile_IsSetAtTheWeightOnItsAxis(FontWeight weight, float em)
    {
        var measured = new CoreTextService().Measure("Handgloves", Body with { Family = VariableFace, Weight = weight }, 1f,
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
