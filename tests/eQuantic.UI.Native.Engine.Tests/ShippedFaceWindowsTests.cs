using System.Runtime.Versioning;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Shell.Windows.Graphics;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A face the app ships, set by DirectWrite (#1055), on the one machine kind that can answer: the
/// shell hands the family's files to DirectWrite from memory, through an in-memory font file loader,
/// a font set and a collection, every one of them reached by a hand-numbered vtable slot. A wrong
/// slot is a call into the neighbouring function, so this measures and draws in the face and asserts
/// what came back. The face is the tests' own: every glyph one em wide.
/// </summary>
[SupportedOSPlatform("windows")]
public class ShippedFaceWindowsTests
{
    private const string Assembly = "eQuantic.UI.Native.Engine.Tests";

    private static readonly FontFamily TestFace = new("eQuantic Test Face",
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400),
        new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Italic.ttf", 400, 400, Italic: true));

    private static readonly TypeStyle Body = new(17, 22, FontWeight.Regular, 0, 1.4f);

    public ShippedFaceWindowsTests() => FaceResolution.Clear();

    [WindowsFact]
    public void AFaceTheAppShips_MeasuresAsThatFace_AndNothingIsReported()
    {
        using var text = new DirectWriteTextService();

        var shipped = text.Measure("Handgloves", Body with { Family = TestFace }, 1f, float.PositiveInfinity, 1);

        shipped.Width.Should().BeApproximately(10 * 17, 0.5f, "every glyph of the shipped face is one em wide");
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    [WindowsFact]
    public void AFaceTheAppShips_IsDrawnInThatFace_ItsItalicToo()
    {
        using var text = new DirectWriteTextService();

        var upright = text.Rasterize("Handgloves", Body with { Family = TestFace }, 1f, float.PositiveInfinity, 1, 1f, TextAlignment.Start)!;
        var italic = text.Rasterize("Handgloves", Body with { Family = TestFace, Italic = true }, 1f, float.PositiveInfinity, 1, 1f, TextAlignment.Start)!;

        ShippedFacePhotonTests.InkColumns(upright).Should().BeGreaterThan(140);
        ShippedFacePhotonTests.InkColumns(italic).Should().BeGreaterThan(140);
        FaceResolution.Unresolved.Should().BeEmpty();
    }

    [WindowsFact]
    public void AFileItsAssemblyDoesNotHold_IsReported_AndTheTextFallsBack()
    {
        using var text = new DirectWriteTextService();
        var missing = new FontFamily("Nowhere Sans", new FontFile(Assembly, "eQuantic.UI.Fonts/Nowhere.ttf", 400, 400));

        var measured = text.Measure("Handgloves", Body with { Family = missing }, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().Equal(["Nowhere Sans"]);
        measured.Width.Should().BeLessThan(10 * 17 * 0.8f, "the platform's face is narrower than the shipped one");
    }

    /// <summary>A family resolves from its files WHOLE or not at all: half of one is reported.</summary>
    [WindowsFact]
    public void AFamilyMissingOneOfItsFiles_IsReported_AndTheTextFallsBack()
    {
        using var text = new DirectWriteTextService();
        var half = new FontFamily("eQuantic Test Face",
            new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Regular.ttf", 400, 400),
            new FontFile(Assembly, "eQuantic.UI.Fonts/TestFace-Missing-Italic.ttf", 400, 400, Italic: true));

        var measured = text.Measure("Handgloves", Body with { Family = half }, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().Equal(["eQuantic Test Face"]);
        measured.Width.Should().BeLessThan(10 * 17 * 0.8f, "the platform's face, not the half that registered");
    }

    /// <summary>
    /// A shipped family whose file is missing is reported even when the machine has a family of that
    /// name: resolving the installed Arial would hide, on this machine alone, a packaging error that
    /// every other machine shows.
    /// </summary>
    [WindowsFact]
    public void AShippedFamilyTheMachineAlsoHas_IsStillReported_WhenItsFileIsMissing()
    {
        using var text = new DirectWriteTextService();
        var arial = new FontFamily("Arial", new FontFile(Assembly, "eQuantic.UI.Fonts/Arial-Missing.ttf", 400, 400));

        text.Measure("Handgloves", Body with { Family = arial }, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().Equal(["Arial"]);
    }
}
