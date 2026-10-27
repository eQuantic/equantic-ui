using System.Runtime.Versioning;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Windows.Graphics;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A family that ships a file per weight is set by DirectWrite in the file <see cref="ShippedFaces.FileFor"/>
/// chooses, as on Apple and on Android (#1099). DirectWrite was handed the whole family and asked for it by
/// name and weight, and it weighs a face by what its file says: two files that say one weight drew both
/// weights from one of them. The families and the widths are <see cref="FontWeightPhotonTests"/>', so the
/// two targets are held to one table.
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

    /// <summary>Google's static Thin and ExtraLight of Inter, as the catalog weighs them: two widths, where
    /// DirectWrite read 250 in both files and set both weights in one of them.</summary>
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
    /// draws, as a browser draws the face a rule declares at 700, never with a bold DirectWrite lays over it
    /// because the file itself says 400.
    /// </summary>
    [WindowsFact]
    public void AFileTheCatalogWeighsHeavierThanItSays_IsDrawnAsItIs()
    {
        using var text = new DirectWriteTextService();
        FontFamily WeighedAt(int weight) => new("eQuantic Weight Face",
            new FontFile(FontWeightPhotonTests.Assembly, "eQuantic.UI.Fonts/weights/WeightFace-400.ttf", weight, weight));
        byte[] Coverage(FontFamily family, FontWeight weight) => text.Rasterize("Handgloves",
            Body with { Family = family, Weight = weight }, 1f, float.PositiveInfinity, 1, 2f, TextAlignment.Start)!.Alpha;

        Coverage(WeighedAt(700), FontWeight.Bold).Should().Equal(Coverage(WeighedAt(400), FontWeight.Regular));
        FaceResolution.Unresolved.Should().BeEmpty();
    }
}
