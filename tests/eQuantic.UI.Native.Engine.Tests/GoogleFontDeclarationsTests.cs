using eQuantic.UI.Build;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A family from Google Fonts is declared in C# and read from the syntax of the app's files before the
/// app compiles (#1057): the family a literal, each weight a member of <c>FontWeight</c>, and anything the
/// build cannot read before the compile stops it at the attribute.
/// </summary>
public sealed class GoogleFontDeclarationsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("eq-google-fonts-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void AFamily_IsReadWithEachWeightAndItsItalic_AndWhereItIsDeclared()
    {
        var file = Source("Program.cs", """
            using eQuantic.UI.Primitives;

            [assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold, Italic = true)]
            """);

        var (declarations, errors) = GoogleFontDeclarations.Read([file]);

        errors.Should().BeEmpty();
        declarations.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Family = "Inter",
            Cuts = new[] { new FontCut(400, false), new FontCut(700, false), new FontCut(400, true), new FontCut(700, true) },
            Place = $"{file}(3,12)",
        });
    }

    [Fact]
    public void AFamilyNamingNoWeight_ShipsItsRegular_HoweverTheAttributeIsSpelt()
    {
        var file = Source("Fonts.cs", """
            [assembly: global::eQuantic.UI.Primitives.GoogleFontAttribute("JetBrains Mono")]
            [assembly: eQuantic.UI.Primitives.GoogleFont(family: "Bebas Neue", eQuantic.UI.Primitives.FontWeight.Regular, Italic = false)]
            """);

        var (declarations, errors) = GoogleFontDeclarations.Read([file]);

        errors.Should().BeEmpty();
        declarations.Select(declaration => (declaration.Family, declaration.Cuts.Single())).Should().Equal(
            ("JetBrains Mono", new FontCut(400, false)),
            ("Bebas Neue", new FontCut(400, false)));
    }

    [Fact]
    public void AFamilyDeclaredTwice_ShipsTheCutsOfBoth_Once()
    {
        var first = Source("A.cs", """[assembly: GoogleFont("Inter", FontWeight.Regular)]""");
        var second = Source("B.cs", """[assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold)]""");

        var (declarations, _) = GoogleFontDeclarations.Read([first, second]);

        declarations.Should().ContainSingle().Which.Cuts.Should().Equal(new FontCut(400, false), new FontCut(700, false));
    }

    [Fact]
    public void WhatIsNotAnAssemblysGoogleFont_IsNotADeclaration()
    {
        var file = Source("Other.cs", """
            [assembly: System.Reflection.AssemblyMetadata("GoogleFont", "Inter")]

            [GoogleFont("Inter")]
            public sealed class Marked;
            """);

        GoogleFontDeclarations.Read([file]).Declarations.Should().BeEmpty();
    }

    /// <summary>A declaration under <c>#if</c> is read when the compile's symbols say the compiler sees it,
    /// and only then: read without them, the build fetched nothing for a family the app then named.</summary>
    [Fact]
    public void ADeclarationUnderIf_IsReadWithTheCompilesSymbols()
    {
        var file = Source("Fonts.cs", """
            #if BRAND
            [assembly: GoogleFont("Inter")]
            #endif
            """);

        GoogleFontDeclarations.Read([file], ["DEBUG", "BRAND"]).Declarations.Should().ContainSingle();
        GoogleFontDeclarations.Read([file], ["DEBUG"]).Declarations.Should().BeEmpty();
    }

    [Fact]
    public void TwoFamiliesKeptUnderOneName_StopTheBuild()
    {
        var file = Source("Fonts.cs", """
            [assembly: GoogleFont("Roboto Mono")]
            [assembly: GoogleFont("RobotoMono")]
            """);

        GoogleFontDeclarations.Read([file]).Errors.Should().ContainSingle().Which.Should().Contain("error EQ4007: the families 'Roboto Mono' and 'RobotoMono' would keep their files under one name");
    }

    [Theory]
    [InlineData("[assembly: GoogleFont(Brand.Family)]", "written out as a string literal")]
    [InlineData("[assembly: GoogleFont(\"Inter\", (FontWeight)300)]", "written out as a member of FontWeight")]
    [InlineData("[assembly: GoogleFont(\"Inter\", 700)]", "written out as a member of FontWeight")]
    [InlineData("[assembly: GoogleFont(\"Inter\", Italic = Brand.Italic)]", "written out as true or false")]
    [InlineData("[assembly: GoogleFont(\"\")]", "names no family")]
    public void ADeclarationTheBuildCannotReadBeforeTheCompile_StopsItAtTheAttribute(string attribute, string expected)
    {
        var file = Source("Brand.cs", attribute);

        var (declarations, errors) = GoogleFontDeclarations.Read([file]);

        declarations.Should().BeEmpty();
        errors.Should().ContainSingle().Which.Should().StartWith($"{file}(1,12): error EQ4007: ").And.Contain(expected);
    }

    private string Source(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }
}
