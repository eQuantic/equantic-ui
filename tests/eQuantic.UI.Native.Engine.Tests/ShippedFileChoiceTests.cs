using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The file of a family every shell sets text in, chosen as a browser chooses among a family's faces
/// (CSS Fonts §5.2), so a family draws from the same file on the web, on Apple and on Android (#1082).
/// </summary>
public class ShippedFileChoiceTests
{
    private static FontFile File(int weight, bool italic = false) => new("Acme.App", $"eQuantic.UI.Fonts/Face-{weight}{(italic ? "i" : "")}.ttf", weight, weight, italic);

    private static readonly FontFamily Three = new("Face", File(300), File(400), File(700));

    [Theory]
    [InlineData(FontWeight.Light, 300)]
    [InlineData(FontWeight.Regular, 400)]
    [InlineData(FontWeight.Bold, 700)]
    public void AWeightAFileSets_IsSetInThatFile(FontWeight weight, int file) =>
        ShippedFaces.FileFor(Three, weight, italic: false)!.MinWeight.Should().Be(file);

    [Theory]
    [InlineData(FontWeight.Thin, 300)]       // below 400: lighter first, none, then the nearest heavier
    [InlineData(FontWeight.ExtraLight, 300)]
    [InlineData(FontWeight.Medium, 400)]     // 400 to 500: heavier up to 500, none, then the nearest lighter
    [InlineData(FontWeight.SemiBold, 700)]   // above 500: heavier first
    [InlineData(FontWeight.ExtraBold, 700)]  // above 500: heavier first, none, then the nearest lighter
    [InlineData(FontWeight.Black, 700)]
    public void AWeightNoFileSets_IsSetInTheFileABrowserWouldChoose(FontWeight weight, int file) =>
        ShippedFaces.FileFor(Three, weight, italic: false)!.MinWeight.Should().Be(file);

    /// <summary>From 400 to 500 the three steps keep their order: the heavier files up to 500, then the
    /// lighter, then the heavier past 500.</summary>
    [Fact]
    public void AWeightFrom400To500_LooksUpTo500_ThenLighter_ThenPast500()
    {
        ShippedFaces.FileFor(new FontFamily("Face", File(300), File(500), File(700)), FontWeight.Regular, italic: false)!
            .MinWeight.Should().Be(500, "a heavier file up to 500 comes first");
        ShippedFaces.FileFor(new FontFamily("Face", File(300), File(600)), FontWeight.Regular, italic: false)!
            .MinWeight.Should().Be(300, "the lighter files come before the heavier past 500");
    }

    [Fact]
    public void TheSlantComesFirst_AndAFamilyWithNoItalicIsSetUpright()
    {
        var both = new FontFamily("Face", File(400), File(700), File(400, italic: true));

        ShippedFaces.FileFor(both, FontWeight.Bold, italic: true)!.Should().Be(File(400, italic: true), "the italic is the slant asked for");
        ShippedFaces.FileFor(Three, FontWeight.Regular, italic: true)!.Should().Be(File(400), "a family with no italic file is set upright");
    }

    [Fact]
    public void AVariableFile_SetsEveryWeightOnItsAxis()
    {
        var variable = new FontFamily("Face", new FontFile("Acme.App", "eQuantic.UI.Fonts/Face.ttf", 100, 900));

        ShippedFaces.FileFor(variable, FontWeight.Thin, italic: false)!.MaxWeight.Should().Be(900);
        ShippedFaces.FileFor(variable, FontWeight.Black, italic: false)!.MinWeight.Should().Be(100);
    }

    [Fact]
    public void AFamilyWithNoFiles_HasNoFile() =>
        ShippedFaces.FileFor(new FontFamily("Georgia"), FontWeight.Regular, italic: false).Should().BeNull();
}
