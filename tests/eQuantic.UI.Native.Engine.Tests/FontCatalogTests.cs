using eQuantic.UI.Build;
using eQuantic.UI.Tests;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>A name the Macintosh platform alone carries is Mac Roman, where an <c>é</c> is
    /// <c>0x8E</c>: read as Latin-1 it was a control character, and the build refused a valid font.</summary>
    [Fact]
    public void AFamilyNamedOnlyForTheMac_IsReadInMacRoman()
    {
        OpenTypeFace.Read(TestFont.Build("Café Display", names: TestFont.NameRecords.Macintosh)).Family.Should().Be("Café Display");
    }

    /// <summary>A symbol font names itself in Windows Symbol records alone, UTF-16BE as every Windows
    /// record is: skipped, its family read as none and the build refused a valid font.</summary>
    [Fact]
    public void AFamilyNamedOnlyForWindowsSymbol_IsRead()
    {
        OpenTypeFace.Read(TestFont.Build("Brand Glyphs", names: TestFont.NameRecords.WindowsSymbol)).Family
            .Should().Be("Brand Glyphs");
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

    /// <summary>
    /// A family named like the class, or like a member every class inherits, takes <c>Family</c> after
    /// it, and the catalog compiles as the app's build compiles it: <c>Fonts.Fonts</c> was CS0542, and a
    /// <c>ToString</c> property hid object's.
    /// </summary>
    [Fact]
    public void AFamilyNamedLikeTheClassOrAnInheritedMember_TakesFamilyAfterIt()
    {
        var output = Path.Combine(_dir, "Fonts.g.cs");
        var (families, errors) = FontCatalog.Write(
        [
            Source("Fonts.ttf", TestFont.Build("Fonts")),
            Source("ToString.ttf", TestFont.Build("To String")),
            Source("Brand.ttf", TestFont.Build("Brand Sans")),
        ], "Acme.App", output, "Acme.App");

        (families, errors.Count).Should().Be((3, 0));
        var code = File.ReadAllText(output);
        code.Should().Contain("public static FontFamily FontsFamily { get; } = new(\"Fonts\",")
            .And.Contain("public static FontFamily ToStringFamily { get; } = new(\"To String\",")
            .And.Contain("public static FontFamily BrandSans { get; } = new(\"Brand Sans\",");
        Compiled(code).Should().BeEmpty("the catalog is compiled with the app, warnings as errors where the app says so");
        Compiled(code.Replace("FontsFamily", "Fonts")).Should().Contain(diagnostic => diagnostic.Contains("CS0542"),
            "the compile above would have said so");
    }

    /// <summary>A family with no letter or digit has no member to give, and stops the build naming its
    /// file: it wrote a member with no name, and the app's compile failed in a file it never wrote.</summary>
    [Fact]
    public void AFamilyWithNoLetterOrDigit_StopsTheBuildNamingIt()
    {
        var symbols = Source("Plus.ttf", TestFont.Build("++"));

        var (_, errors) = FontCatalog.Write([symbols], "Acme.App", Path.Combine(_dir, "Fonts.g.cs"), "Acme.App");

        errors.Should().ContainSingle().Which.Should().StartWith($"{symbols.Path} : error EQ4005: Plus.ttf is not a face")
            .And.Contain("'++', has no letter or digit");
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

    /// <summary>
    /// A static face's weight is the one position its STAT table gives it on <c>wght</c>, whatever its
    /// weight class says: Google's Thin and ExtraLight of Inter both carry 250 there, the value Windows's
    /// GDI needs, and 100 and 200 in their STAT (#1082). A STAT that gives several positions describes a
    /// family, and the weight class stands; a position flagged as an older sibling's is another font's.
    /// </summary>
    [Fact]
    public void AStaticFacesStatPosition_IsItsWeight_WhateverItsWeightClassSays()
    {
        OpenTypeFace.Read(TestFont.Build("Inter", weight: 250, statWeights: [100]))
            .Should().Be(new OpenTypeFace("Inter", 100, 100, false));
        OpenTypeFace.Read(TestFont.Build("Inter", weight: 250, statWeights: [100, 200]))
            .Should().Be(new OpenTypeFace("Inter", 250, 250, false), "a STAT with several positions describes the family");
        OpenTypeFace.Read(TestFont.Build("Inter", weight: 250, statWeights: [100], statSiblingWeights: [200]))
            .Should().Be(new OpenTypeFace("Inter", 100, 100, false), "an older sibling's position is another font's");
        OpenTypeFace.Read(TestFont.Build("Inter", weight: 300, statSiblingWeights: [200]))
            .Should().Be(new OpenTypeFace("Inter", 300, 300, false), "a STAT that places only a sibling says nothing of this face");
    }

    [Fact]
    public void AThinAndAnExtraLightThatShareAWeightClass_AreTwoFacesOfTheCatalog()
    {
        var (families, errors) = FontCatalog.Write(
        [
            Source("Inter-100.ttf", TestFont.Build("Inter", weight: 250, statWeights: [100])),
            Source("Inter-200.ttf", TestFont.Build("Inter", weight: 250, statWeights: [200])),
        ], "Acme.App", Path.Combine(_dir, "Fonts.g.cs"), "Acme.App");

        errors.Should().BeEmpty();
        families.Should().Be(1);
        File.ReadAllText(Path.Combine(_dir, "Fonts.g.cs"))
            .Should().Contain("\"eQuantic.UI.Fonts/Inter-100.ttf\", 100, 100)").And.Contain("\"eQuantic.UI.Fonts/Inter-200.ttf\", 200, 200)");
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

    /// <summary>
    /// The SDK hands each file as two lines, its path and its resource name, and a path is a line whole:
    /// one line a file was cut at its first bar, which a folder on macOS and Linux may hold, and the
    /// file was passed over in silence.
    /// </summary>
    [SkippableFact]
    public void TheList_IsTwoLinesAFile_AndAPathKeepsItsBar()
    {
        Skip.If(OperatingSystem.IsWindows(), "a bar is not a character of a Windows path");
        var folder = Directory.CreateDirectory(Path.Combine(_dir, "brand|probe")).FullName;
        var font = Path.Combine(folder, "Probe.ttf");
        File.WriteAllBytes(font, TestFont.Build("Acme Probe"));

        FontCatalog.ReadList(List(font, "eQuantic.UI.Fonts/Probe.ttf", ""))
            .Should().Equal(new FontCatalog.Source(font, "eQuantic.UI.Fonts/Probe.ttf"));
    }

    /// <summary>A list that does not hold stops the build, never read as fewer files: a blank line inside
    /// it, a path with no resource name after it, and a path to nothing.</summary>
    [Fact]
    public void AListThatDoesNotHold_IsRefusedSayingWhere()
    {
        var font = Path.Combine(_dir, "Probe.ttf");
        File.WriteAllBytes(font, TestFont.Build("Acme Probe"));

        new Action(() => FontCatalog.ReadList(List(font, "", "eQuantic.UI.Fonts/Probe.ttf")))
            .Should().Throw<InvalidDataException>().WithMessage("line 2 of the list of fonts is blank*");
        new Action(() => FontCatalog.ReadList(List(font, "eQuantic.UI.Fonts/Probe.ttf", font)))
            .Should().Throw<InvalidDataException>().WithMessage("line 3 of the list of fonts is a file with no resource name after it*");
        new Action(() => FontCatalog.ReadList(List(Path.Combine(_dir, "Gone.ttf"), "eQuantic.UI.Fonts/Gone.ttf")))
            .Should().Throw<FileNotFoundException>().WithMessage("the font line 1 of the list names is not there*");
    }

    /// <summary>A list file holding <paramref name="lines"/>, one a line.</summary>
    private string List(params string[] lines)
    {
        var list = Path.Combine(_dir, "fonts.txt");
        File.WriteAllLines(list, lines);
        return list;
    }

    /// <summary>The warnings and errors of <paramref name="code"/> compiled against the vocabulary, as
    /// the app's build compiles the catalog.</summary>
    private static IEnumerable<string> Compiled(string code) =>
        CSharpCompilation.Create("Acme.App", [CSharpSyntaxTree.ParseText(code)],
                ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                    .Select(path => TestReferences.Of(path)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString());

    private FontCatalog.Source Source(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return new FontCatalog.Source(path, "eQuantic.UI.Fonts/" + name);
    }
}
