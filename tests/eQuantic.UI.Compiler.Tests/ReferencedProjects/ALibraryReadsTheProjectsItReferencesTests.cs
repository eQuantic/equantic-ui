using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// The code of a library the app references reads the projects that library references, as the C#
/// compiler reads them (#887).
/// <para>
/// The projects were compiled in the order their ASSEMBLIES gave, each against the ones it reached,
/// and an assembly names a project only where its metadata kept a trace of it. A constant read from
/// another project is copied into the reader, and a reference assembly, which is what the SDK hands,
/// keeps no method body. Cura's shared library read a constant of its theme project and nothing else
/// of it: it was compiled without that project, and the name failed to bind (EQ2006). The order is
/// the restore graph's now, which the app's assets file holds.
/// </para>
/// </summary>
public sealed class ALibraryReadsTheProjectsItReferencesTests
{
    private const string Fonts =
        """
        namespace Theme;

        public static class Fonts
        {
            public const string Serif = "Newsreader";
        }
        """;

    private const string Ruler =
        """
        namespace Theme;

        public sealed class Ruler
        {
            public int Step => 4;
        }
        """;

    /// <summary>The library reads the constant through a constant of its own, as Cura's does, and the
    /// class only inside a method: its reference assembly names the theme project nowhere.</summary>
    private const string Typography =
        """
        using Theme;

        namespace Common;

        public static class Typography
        {
            private const string Serif = Fonts.Serif;

            public static string Family() => Serif;

            public static int Spacing() => new Ruler().Step * 2;
        }
        """;

    private const string Home =
        """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text($"{Common.Typography.Family()} {Common.Typography.Spacing()}", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private static Solution Solution() =>
        new Solution()
            .Library("Theme", files: [("Fonts.cs", Fonts), ("Ruler.cs", Ruler)])
            .Library("Common", references: ["Theme"], files: [("Typography.cs", Typography)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", Home)]);

    [Fact]
    public void TheLibraryReadsAConstantAndATypeOfTheProjectItReferences()
    {
        using var solution = Solution();
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty("the library's code binds as the C# compiler bound it");
        built.Module("Typography").Should().NotBeNull($"the page reaches the library's class. Modules: {string.Join(", ", built.Modules)}. Home: {built.Module("Home")}");
        built.SiblingImportsOf("Typography").Should().Contain("Ruler",
            "the class the library uses inside a method is a module the library's module imports");
    }

    [Fact]
    public void TheLibraryReadsAProject_ReachedThroughOneWithNoSourceOfItsOwn()
    {
        // Common references Facade, and Facade references Theme: references flow, so Common's code
        // reads Theme. Facade holds no C# of its own, so the build keeps its assembly and compiles
        // nothing for it. The walk over the graph still goes through it: stopped there, it never
        // reached Theme, whose assembly is not among the references either, since Theme is compiled.
        using var solution = new Solution()
            .Library("Theme", files: [("Fonts.cs", Fonts), ("Ruler.cs", Ruler)])
            .Library("Facade", references: ["Theme"])
            .Library("Common", references: ["Facade"], files: [("Typography.cs", Typography)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", Home)]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty("the library's code binds as the C# compiler bound it");
        built.SiblingImportsOf("Typography").Should().Contain("Ruler");
    }

    [Fact]
    public void AGraphWithoutTheEdge_CompilesTheLibraryWithoutTheProject()
    {
        // The control: the same solution, handed an assets file that says Common references nothing.
        // The build follows the graph and nothing else, so the library's names fail to bind again.
        using var solution = Solution();
        var built = solution.Build(graph: new Dictionary<string, string[]> { ["Common"] = [] });

        built.Failed.Should().BeTrue();
        built.ErrorsIn("Common").Should().Contain(line => line.Contains("EQ2006"),
            "with no edge to the theme project, the library is compiled without it");
    }

    [Fact]
    public void ProjectsHandedWithNoGraph_StopTheBuild()
    {
        // Read as no edge at all, a missing graph would compile every library without the projects it
        // reads, which is the defect this exists to end.
        using var solution = Solution();
        var built = solution.Build(handGraph: false);

        built.Failed.Should().BeTrue();
        built.Errors.Should().Contain("EQ0001").And.Contain("--project-assets");
    }
}
