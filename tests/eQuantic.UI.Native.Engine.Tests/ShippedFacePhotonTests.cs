using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A face the app ships, set by CoreText on a Mac that never installed it (#1055): the shell
/// registers the family's files the first time text asks for it, read from the assembly they are
/// embedded in, and the text measures as that face does. The face is the tests' own (TestFont):
/// every glyph one em wide, which no installed face is, so the width says which face set the text.
/// </summary>
public class ShippedFacePhotonTests
{
    private const string Assembly = "eQuantic.UI.Native.Engine.Tests";

    private static readonly FontFamily TestFace = new("eQuantic Test Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400),
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Italic.ttf", 400, 400, Italic: true));

    private static readonly TypeStyle Body = new(17, 22, FontWeight.Regular, 0, 1.4f);

    public ShippedFacePhotonTests() => FaceResolution.Clear();

    [MacFact]
    public void AFaceTheAppShips_MeasuresAsThatFace_AndNothingIsReported()
    {
        var shipped = new CoreTextService().Measure("Handgloves", Body with { Family = TestFace }, 1f, float.PositiveInfinity, 1);

        shipped.Width.Should().BeApproximately(10 * 17, 0.5f, "every glyph of the shipped face is one em wide");
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    /// <summary>Drawn in it too, not only measured: ten boxes, each nine tenths of an em, are ink
    /// no installed face puts on "Handgloves".</summary>
    [MacFact]
    public void AFaceTheAppShips_IsDrawnInThatFace()
    {
        var raster = new CoreTextService().Rasterize("Handgloves", Body with { Family = TestFace }, 1f,
            float.PositiveInfinity, 1, 1f, TextAlignment.Start)!;

        InkColumns(raster).Should().BeGreaterThan(140);
    }

    [MacFact]
    public void ItsItalicCut_IsTheItalicFile()
    {
        var italic = new CoreTextService().Measure("Handgloves", Body with { Family = TestFace, Italic = true }, 1f, float.PositiveInfinity, 1);

        italic.Width.Should().BeApproximately(10 * 17, 0.5f, "the italic file is shipped too, and is as wide");
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    [MacFact]
    public void AFileItsAssemblyDoesNotHold_IsReported_AndTheTextFallsBack()
    {
        var missing = new FontFamily("Nowhere Sans", new FontFile(Assembly, "eQuantic.UI.Fonts/Nowhere.ttf", 400, 400));

        var measured = new CoreTextService().Measure("Handgloves", Body with { Family = missing }, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().Equal(["Nowhere Sans"]);
        measured.Width.Should().BeLessThan(10 * 17 * 0.8f, "the platform's face is narrower than the shipped one");
    }

    /// <summary>
    /// A family resolves from its files WHOLE or not at all. Its upright file is the shipped face's,
    /// registered in this process by the cases above, and its italic one is not there: it is reported
    /// and set in the platform's face, rather than drawn from the half that registered.
    /// </summary>
    [MacFact]
    public void AFamilyMissingOneOfItsFiles_IsReported_AndTheTextFallsBack()
    {
        var half = new FontFamily("eQuantic Test Face",
            new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400),
            new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Missing-Italic.ttf", 400, 400, Italic: true));

        var measured = new CoreTextService().Measure("Handgloves", Body with { Family = half }, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().Equal(["eQuantic Test Face"]);
        measured.Width.Should().BeLessThan(10 * 17 * 0.8f, "the platform's face, not the half that registered");
    }

    /// <summary>How many columns of the raster carry any ink.</summary>
    internal static int InkColumns(TextRaster raster) =>
        Enumerable.Range(0, raster.Width).Count(x =>
            Enumerable.Range(0, raster.Height).Any(y => raster.Alpha[y * raster.Width + x] > 0));
}
