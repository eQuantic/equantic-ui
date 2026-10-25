using eQuantic.UI.Build;
using eQuantic.UI.Tests;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An app's faces are read from their files (#1055): the family, the weights and the italic come
/// out of the file itself, the catalog writes one typed member per family, and a file the SDK cannot
/// ship stops the build naming it.
/// </summary>
public sealed class FontCatalogTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("eq-fonts-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void AFont_SaysItsFamilyItsWeightAndWhetherItIsItalic()
    {
        OpenTypeFace.Read(TestFont.Build("eQuantic Test Face"))
            .Should().Be(new OpenTypeFace("eQuantic Test Face", 400, 400, false));
        OpenTypeFace.Read(TestFont.Build("eQuantic Test Face", weight: 700, italic: true))
            .Should().Be(new OpenTypeFace("eQuantic Test Face", 700, 700, true));
    }

    [Fact]
    public void AnObliqueFont_IsTheItalicCut()
    {
        OpenTypeFace.Read(TestFont.Build("eQuantic Test Face", oblique: true)).Italic
            .Should().BeTrue("a slanted roman is the cut italic text is set in");
    }

    [Fact]
    public void AVariableFont_SaysTheRangeOfItsWeightAxis()
    {
        OpenTypeFace.Read(TestFont.Build("eQuantic Test Face", weightAxis: (200, 800)))
            .Should().Be(new OpenTypeFace("eQuantic Test Face", 200, 800, false));
    }

    [Theory]
    [InlineData("ttcf", "collection")]
    [InlineData("wOF2", "web font")]
    [InlineData("wOFF", "web font")]
    [InlineData("<svg", "not an OpenType font")]
    public void WhatIsNotAnOpenTypeFace_IsRefusedWithItsReason(string signature, string reason)
    {
        var bytes = new byte[64];
        System.Text.Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);

        new Action(() => OpenTypeFace.Read(bytes)).Should().Throw<FormatException>().WithMessage($"*{reason}*");
    }

    [Fact]
    public void AFontCutShort_IsRefused()
    {
        var font = TestFont.Build("eQuantic Test Face");

        new Action(() => OpenTypeFace.Read(font.AsSpan(0, font.Length / 2))).Should().Throw<FormatException>();
    }

    [Fact]
    public void TheCatalog_HasAMemberPerFamily_WithItsFilesAsTheyAreEmbedded()
    {
        var (families, errors) = FontCatalog.Write(
        [
            Source("Atkinson-Italic.ttf", TestFont.Build("Atkinson Hyperlegible Next", italic: true, weightAxis: (200, 800))),
            Source("Atkinson.ttf", TestFont.Build("Atkinson Hyperlegible Next", weightAxis: (200, 800))),
            Source("Mono.ttf", TestFont.Build("Brand Mono")),
        ], "Acme.App", Path.Combine(_dir, "Fonts.g.cs"), "Acme.App");

        errors.Should().BeEmpty();
        families.Should().Be(2);
        var code = File.ReadAllText(Path.Combine(_dir, "Fonts.g.cs"));
        code.Should().Contain("[assembly: FontCatalog(typeof(Acme.App.Fonts))]")
            .And.Contain("public static FontFamily AtkinsonHyperlegibleNext { get; } = new(\"Atkinson Hyperlegible Next\",")
            .And.Contain("new FontFile(\"Acme.App\", \"eQuantic.UI.Fonts/Atkinson.ttf\", 200, 800),")
            .And.Contain("new FontFile(\"Acme.App\", \"eQuantic.UI.Fonts/Atkinson-Italic.ttf\", 200, 800, Italic: true));")
            .And.Contain("public static FontFamily BrandMono { get; } = new(\"Brand Mono\",");
        code.IndexOf("Atkinson.ttf", StringComparison.Ordinal).Should()
            .BeLessThan(code.IndexOf("Atkinson-Italic.ttf", StringComparison.Ordinal), "the upright file comes first");
    }

    [Fact]
    public void NoFonts_WriteNoCatalog()
    {
        var (families, errors) = FontCatalog.Write([], "Acme.App", Path.Combine(_dir, "Fonts.g.cs"), "Acme.App");

        (families, errors.Count).Should().Be((0, 0));
        File.Exists(Path.Combine(_dir, "Fonts.g.cs")).Should().BeFalse();
    }

    [Fact]
    public void AFileThatIsNotAFont_StopsTheBuildNamingIt()
    {
        var junk = Source("Broken.ttf", "not a font at all, only text"u8.ToArray());

        var (_, errors) = FontCatalog.Write([junk], "Acme.App", Path.Combine(_dir, "Fonts.g.cs"), "Acme.App");

        errors.Should().ContainSingle().Which.Should().StartWith($"{junk.Path} : error EQ4005: Broken.ttf is not a face");
        File.Exists(Path.Combine(_dir, "Fonts.g.cs")).Should().BeFalse("a catalog that leaves a file out is a face that falls back in silence");
    }

    [Fact]
    public void TwoFilesThatClaimOneFace_StopTheBuild()
    {
        var (_, errors) = FontCatalog.Write(
        [
            Source("Regular.ttf", TestFont.Build("Brand Sans", weight: 400)),
            Source("Variable.ttf", TestFont.Build("Brand Sans", weightAxis: (100, 900))),
            Source("Italic.ttf", TestFont.Build("Brand Sans", italic: true)),
        ], "Acme.App", Path.Combine(_dir, "Fonts.g.cs"), "Acme.App");

        errors.Should().ContainSingle().Which.Should().Contain("error EQ4006").And.Contain("'Brand Sans'")
            .And.Contain("Regular.ttf").And.Contain("Variable.ttf");
    }

    private FontCatalog.Source Source(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return new FontCatalog.Source(path, "eQuantic.UI.Fonts/" + name);
    }
}
