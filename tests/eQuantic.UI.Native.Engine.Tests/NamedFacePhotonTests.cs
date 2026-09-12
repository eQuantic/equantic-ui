using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A named face on Photon, and the half that makes it honest: a face the machine does not have is
/// REPORTED rather than silently substituted. Every text engine substitutes — CoreText, DirectWrite
/// and Android all do — which is right for them and wrong for a design handoff, where the whole
/// point is that the metrics were drawn against a specific family.
/// <para>
/// The instrument was wrong on the first attempt, which is why it is pinned: comparing the resolved
/// family name to the requested one reports a false miss, because asking for "JetBrainsMono NF"
/// hands back "JetBrainsMono Nerd Font" — the same font under its canonical name. A descriptor
/// MATCH answers the question without a string for anyone to compare.
/// </para>
/// </summary>
public class NamedFacePhotonTests
{
    private static readonly TypeStyle Body = new(17, 22, FontWeight.Regular, 0, 1.4f);

    public NamedFacePhotonTests() => FaceResolution.Clear();

    [MacFact]
    public void NoFaceNamed_ReportsNothing()
    {
        new CoreTextService().Measure("Handgloves", Body, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().BeEmpty("the platform's own face is always there");
    }

    [MacFact]
    public void AFaceTheMachineDoesNotHave_IsNamed()
    {
        new CoreTextService().Measure("Handgloves", Body with { Family = "No Such Face Anywhere" },
            1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().Equal(["No Such Face Anywhere"]);
    }

    /// <summary>
    /// The false-positive case that killed the first instrument. This machine has the Nerd Font cut,
    /// whose canonical family name is not the one a font list reports, and a name comparison called
    /// a correct resolution a miss.
    /// </summary>
    [MacFact]
    public void AFaceUnderADifferentCanonicalName_IsStillFound()
    {
        var installed = new CoreTextService();
        installed.Measure("Handgloves", Body with { Family = "JetBrainsMono NF" }, 1f, float.PositiveInfinity, 1);

        FaceResolution.Unresolved.Should().BeEmpty(
            "\"JetBrainsMono NF\" resolves to \"JetBrainsMono Nerd Font\", which is the same font");
    }

    [MacFact]
    public void ANamedFace_ActuallyMeasuresDifferently()
    {
        var service = new CoreTextService();
        var system = service.Measure("Handgloves 0123", Body, 1f, float.PositiveInfinity, 1);
        var named = service.Measure("Handgloves 0123", Body with { Family = "JetBrainsMono NF" },
            1f, float.PositiveInfinity, 1);

        // A monospaced face advances every glyph equally, so the same string is a different width.
        named.Width.Should().NotBe(system.Width, "the face reached the measurer, not only the report");
    }

    [Fact]
    public void TheTally_IsRunScoped_LikeTheContainedOne()
    {
        FaceResolution.Missing("Ghost Sans");
        FaceResolution.Missing("Ghost Sans");
        FaceResolution.Unresolved.Should().Equal(["Ghost Sans"], "a missing face is missing on every glyph");

        FaceResolution.Clear();
        FaceResolution.Unresolved.Should().BeEmpty();
    }
}
