using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// Each project the app references is a compilation of its own, which the app references as a
/// compilation in place of the project's assembly (#624). That one fact is what makes a type of a
/// referenced project a type declared in source, which is how the compiler tells a type it writes a
/// module for. And it is one compilation PER PROJECT, never the projects' sources added to the app's:
/// each keeps its own <c>global using</c> directives and its own internal types.
/// </summary>
public sealed class ReferencedProjectCompilationTests
{
    [Fact]
    public void ATypeOfAReferencedProject_IsDeclaredInSource_InItsOwnFile()
    {
        using var solution = SharedLibrary.Solution();
        var built = solution.Build();

        var cursor = built.Compilation.GetTypeByMetadataName("Common.Cursor")!;
        cursor.Locations.Should().OnlyContain(location => location.IsInSource,
            "read through its project's compilation, the record has a source");
        cursor.DeclaringSyntaxReferences.Should().ContainSingle()
            .Which.SyntaxTree.FilePath.Should().Be(solution.FileOf("Common", "Cursor.cs"));

        // The project reached through another one is a compilation too: the app's reference list
        // holds its assembly, since references flow.
        built.Compilation.GetTypeByMetadataName("Domain.Money")!.Locations
            .Should().OnlyContain(location => location.IsInSource);
    }

    [Fact]
    public void HandedNoProjects_TheSameTypeHasNoSource()
    {
        // The control, and the whole of #624: the type reaches the build as the assembly MSBuild
        // resolved, and no rule that asks for a source can see it.
        using var solution = SharedLibrary.Solution();
        var built = solution.Build(handProjects: false);

        var cursor = built.Compilation.GetTypeByMetadataName("Common.Cursor")!;
        cursor.Locations.Should().NotContain(location => location.IsInSource);
        cursor.DeclaringSyntaxReferences.Should().BeEmpty();
        built.Modules.Should().NotContain(["Cursor", "Palette", "Wordmark", "Receipt", "Money"],
            "a type with no source is no module");
    }

    [Fact]
    public void AProjectsAssembly_IsNotAlsoAmongTheReferences()
    {
        // A type read from both its compilation and its assembly is two types of one name, and every
        // use of it is ambiguous: the model binds none, and the call degrades with no diagnostic.
        using var solution = SharedLibrary.Solution();
        var built = solution.Build();

        built.Compilation.References.OfType<PortableExecutableReference>()
            .Select(reference => Path.GetFileName(reference.FilePath))
            .Should().NotContain(["Common.dll", "Domain.dll"]);
        built.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the app binds against the projects' compilations as it does against their assemblies");
    }

    [Fact]
    public void AProjectsGlobalUsing_IsItsOwn_AndDoesNotReachTheApp()
    {
        using var solution = new Solution()
            .Library("Domain", files: [("Money.cs", SharedLibrary.Money)])
            // The library's project file brings Domain into every file of the LIBRARY.
            .Library("Common", references: ["Domain"], globalUsings: "global using Domain;\n", files:
            [
                ("Price.cs", "namespace Common;\n\npublic sealed record Price(Money Amount);\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Common;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(new Price(new Domain.Money(1m, "EUR")).Amount.Currency, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        var common = built.CompilationOf("Common");
        common.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("Price names Money with no using of its own: the library's global using is in force in the library");

        NamesInScope(common, solution.FileOf("Common", "Price.cs"), "Money").Should().NotBeEmpty();
        NamesInScope(built.Compilation, solution.FileOf("App", "Screens/Home.cs"), "Money").Should().BeEmpty(
            "the library's global using reaches no file of the app: added to the app's compilation, it would "
            + "bring Domain into every page, and a name it made ambiguous there binds to nothing");

        built.SiblingImportsOf("Price").Should().Contain("Money");
    }

    [Fact]
    public void AnInternalTypeOfOneFullName_InTheAppAndInAProject_IsTwoTypes()
    {
        const string Guard =
            """
            namespace Shared;

            internal static class Guard
            {
                public static string Tag() => "OWNER";
            }
            """;
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("Guard.cs", Guard.Replace("OWNER", "common")),
                ("Stamp.cs", "namespace Common;\n\npublic static class Stamp\n{\n    public static string Of() => Shared.Guard.Tag();\n}\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Shared/Guard.cs", Guard.Replace("OWNER", "app")),
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(Shared.Guard.Tag(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty();
        built.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the two Guards are in two compilations, as they are in two assemblies");
        built.Module("Guard").Should().Contain("'app'", "the page calls the app's own Guard")
            .And.NotContain("'common'");
        built.Module("Stamp").Should().BeNull("nothing the browser runs reaches the library's Stamp, or its Guard");
    }

    [Fact]
    public void AProjectsFriendDeclaration_StillOpensItsInternalsToTheApp()
    {
        // A project file's InternalsVisibleTo item is written into a generated file under obj, which
        // the C# compiler reads and eqc does not. The declaration is taken from the assembly that
        // carries it, or an internal type the app compiles against would be one the build cannot bind.
        using var solution = new Solution()
            .Library("Common", files:
            [
                (Path.Combine("obj", "Debug", "net10.0", "Common.AssemblyInfo.cs"),
                    "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"App\")]\n"),
                ("Seal.cs", "namespace Common;\n\ninternal sealed record Seal(string Text);\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Common;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(new Seal("kept").Text, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty();
        built.SiblingImportsOf("Home").Should().Contain("Seal");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnAppWhoseAssemblyIsNamedApartFromItsProject_IsTheFriendItsLibraryNames(bool handAssemblyName)
    {
        // A project file may name its assembly apart from itself (<AssemblyName>), and a friend
        // declaration names the ASSEMBLY. The build took the project file's name for the app's
        // compilation, so the library's InternalsVisibleTo("Shop.Site") opened nothing to an app
        // whose file is App.csproj: C# compiled it, and here an internal record it builds was a
        // type the build could not bind. The SDK hands the name MSBuild evaluated (found in review
        // of #827).
        using var solution = new Solution()
            .Library("Common", files:
            [
                (Path.Combine("obj", "Debug", "net10.0", "Common.AssemblyInfo.cs"),
                    "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Shop.Site\")]\n"),
                ("Seal.cs", "namespace Common;\n\ninternal sealed record Seal(string Text);\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, assemblyName: "Shop.Site", files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Common;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(new Seal("kept").Text, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build(handAssemblyName: handAssemblyName);

        if (handAssemblyName)
        {
            built.Failed.Should().BeFalse(built.Errors);
            built.Errors.Should().BeEmpty();
            built.Compilation.AssemblyName.Should().Be("Shop.Site");
            built.SiblingImportsOf("Home").Should().Contain("Seal");
            built.Module("Seal").Should().Contain("export class Seal");
        }
        else
        {
            // The control, and every build before: the app is compiled as "App", a name the library
            // never named. The record is one the model cannot reach, so the call around it does not
            // bind, and code C# compiled is refused here.
            built.Compilation.AssemblyName.Should().Be("App");
            built.Failed.Should().BeTrue();
            built.Errors.Should().Contain("error EQ2006");
            built.Modules.Should().NotContain("Seal");
        }
    }

    /// <summary>What a simple name binds to at the top of a file, as a type or a namespace.</summary>
    private static IEnumerable<ISymbol> NamesInScope(Compilation compilation, string file, string name)
    {
        var tree = compilation.SyntaxTrees.Single(candidate => candidate.FilePath == file);
        var position = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax>().First().SpanStart;
        return compilation.GetSemanticModel(tree).LookupNamespacesAndTypes(position, name: name);
    }
}
