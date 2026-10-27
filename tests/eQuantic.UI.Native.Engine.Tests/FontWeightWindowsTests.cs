using System.Runtime.Versioning;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Windows.Graphics;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A family that ships a file per weight is set by DirectWrite in the file <see cref="ShippedFaces.FileFor"/>
/// chooses, as on Apple and on Android, at the weight the family's catalog gives it (#1099). DirectWrite was
/// handed the whole family and asked for it by name and weight, so it matched by the weights it reads in the
/// files, and laid a simulated bold over a file the catalog weighs bold that declares 400. The families and
/// the widths are <see cref="FontWeightPhotonTests"/>', so the two targets are held to one table.
/// </summary>
[SupportedOSPlatform("windows")]
public class FontWeightWindowsTests
{
    private static readonly TypeStyle Body = new(17, 22, FontWeight.Regular, 0, 1.4f);

    public FontWeightWindowsTests() => FaceResolution.Clear();

    [WindowsTheory]
    [MemberData(nameof(FontWeightPhotonTests.FilePerWeight), MemberType = typeof(FontWeightPhotonTests))]
    public void AFamilyThatShipsAFilePerWeight_IsSetInTheFileOfTheWeightAsked(FontWeight weight, float em)
    {
        using var text = new DirectWriteTextService();

        var measured = text.Measure("Handgloves", Body with { Family = FontWeightPhotonTests.WeightFace, Weight = weight }, 1f,
            float.PositiveInfinity, 1);

        measured.Width.Should().BeApproximately(10 * 17 * em, 0.5f);
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    /// <summary>Google's static Thin and ExtraLight of Inter, as the catalog weighs them: two widths. DirectWrite
    /// told them apart before the shell chose the file too, measured on the Windows runner; this holds the
    /// four targets to it.</summary>
    [WindowsTheory]
    [MemberData(nameof(FontWeightPhotonTests.OneWeightClassTwoWeights), MemberType = typeof(FontWeightPhotonTests))]
    public void TwoFilesThatSayOneWeight_AreEachSetAtTheWeightTheCatalogGivesIt(FontWeight weight, float em)
    {
        using var text = new DirectWriteTextService();

        var measured = text.Measure("Handgloves", Body with { Family = FontWeightPhotonTests.ThinFace, Weight = weight }, 1f,
            float.PositiveInfinity, 1);

        measured.Width.Should().BeApproximately(10 * 17 * em, 0.5f);
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    /// <summary>
    /// The catalog is the one authority on a file's weight: the file it weighs at 700 is drawn as the file
    /// draws, at 700 and at a heavier weight that falls back to it, as a browser draws a bold face a rule
    /// declares, never with a bold DirectWrite lays over it because the file itself says 400.
    /// </summary>
    [WindowsTheory]
    [InlineData(FontWeight.Bold)]
    [InlineData(FontWeight.Black)]
    public void AFileTheCatalogWeighsHeavierThanItSays_IsDrawnAsItIs(FontWeight weight)
    {
        using var text = new DirectWriteTextService();

        Coverage(text, WeighedAt(700), weight).Should().Equal(Coverage(text, WeighedAt(400), FontWeight.Regular));
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    /// <summary>A weight the family does not ship, heavier than its one Regular file, is drawn emboldened, as a
    /// browser draws it: what makes the test above a measurement, since that bold is the one it keeps off.</summary>
    [WindowsFact]
    public void AHeavierWeightThanARegularFileSets_IsEmboldened()
    {
        using var text = new DirectWriteTextService();

        Coverage(text, WeighedAt(400), FontWeight.Bold).Should().NotEqual(Coverage(text, WeighedAt(400), FontWeight.Regular));
    }

    /// <summary>A family of one file, WeightFace's Regular, which declares 400, given <paramref name="weight"/>.</summary>
    private static FontFamily WeighedAt(int weight) => new("eQuantic Weight Face",
        new FontFile(FontWeightPhotonTests.Assembly, "eQuantic.UI.Fonts/weights/WeightFace-400.ttf", weight, weight));

    private static byte[] Coverage(DirectWriteTextService text, FontFamily family, FontWeight weight) => text.Rasterize("Handgloves",
        Body with { Family = family, Weight = weight }, 1f, float.PositiveInfinity, 1, 2f, TextAlignment.Start)!.Alpha;
}
