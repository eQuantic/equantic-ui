using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A served eqc starts each compile from the compilation the one before it loaded, and replaces only
/// the files a save changed (#1278): loading every reference and every file again cost 3.4s of each
/// save. What it writes is held to what a cold build writes after the same edit, byte for byte, and
/// anything a cold load would read differently loads cold.
/// </summary>
public class AWarmBuildTests
{
    private const string Prices =
        """
        namespace Shop;

        public static class Prices
        {
            public const string Label = "Ten coins";
        }
        """;

    /// <summary>A component that reads a constant of ANOTHER file, which its module inlines: the file a
    /// save changes is not the one whose module changes.</summary>
    private const string Tag =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Tag : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => Text(Prices.Label, TypeRole.BodyM);
        }
        """;

    private static Solution Shop() => new Solution()
        .App("Shop", globalUsings: SharedLibrary.Factories, files: [("Prices.cs", Prices), ("Components/Tag.cs", Tag)]);

    /// <summary>
    /// Whether the compiler reported an error. The component is an entry point and the test hands no
    /// bundler, so the build stops at the bundle once every module is written, which is all that is
    /// compared here.
    /// </summary>
    private static string CompilerErrors(Built built) =>
        string.Join('\n', built.Errors.Split('\n').Where(line => line.Contains(": error ", StringComparison.Ordinal)));

    /// <summary>Every module a build wrote, by name, as it wrote it.</summary>
    private static Dictionary<string, string> ModulesOf(Built built) =>
        built.Modules.ToDictionary(name => name, name => built.Module(name)!);

    [Fact]
    public void AWarmBuild_WritesWhatAColdBuildWrites_AfterTheSameEdit()
    {
        using var solution = Shop();
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();
        var before = ModulesOf(cold);
        before.Should().ContainKey("Tag");

        solution.Write("Shop", "Prices.cs", Prices.Replace("Ten coins", "Eleven coins"));
        var (warm, wasWarm) = solution.Rebuild(cold);
        var warmModules = ModulesOf(warm);
        var coldAgain = ModulesOf(solution.Build());

        wasWarm.Should().BeTrue("only a file's text changed, and the compilation before it is kept");
        CompilerErrors(warm).Should().BeEmpty();
        warmModules["Tag"].Should().Contain("Eleven coins",
            "the file the save changed is replaced in the compilation every other file is read in");
        warmModules.Should().Equal(coldAgain, "a warm build writes what a cold build writes after the same edit");
        warmModules["Tag"].Should().NotBe(before["Tag"]);
    }

    private const string Greeting =
        """
        namespace Shop;

        public interface IGreeting
        {
            string Text { get; }
        }

        public sealed class Greeting : IGreeting
        {
            public string Text => "Hello";
        }
        """;

    /// <summary>A component that takes the app's own service through its constructor (#1335).</summary>
    private const string Welcome =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Welcome(IGreeting greeting) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => Text(greeting.Text, TypeRole.BodyM);
        }
        """;

    /// <summary>The registration, as an app writes it: a top-level statement in Program.cs.</summary>
    private const string Registrations =
        """
        using Microsoft.Extensions.DependencyInjection;
        using Shop;

        var services = new ServiceCollection();
        services.AddSingleton<IGreeting, Greeting>();
        """;

    [Fact]
    public void ASaveToTheFileThatRegistersAService_ReachesTheWarmBuild()
    {
        using var solution = new Solution().App("Shop", globalUsings: SharedLibrary.Factories, files:
        [
            ("Program.cs", Registrations),
            ("Greeting.cs", Greeting),
            ("Components/Welcome.cs", Welcome),
        ]);
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();
        cold.Module("Welcome").Should().Contain("$eq.services.provide('IGreeting', () => new Greeting(), 'singleton')");

        solution.Write("Shop", "Program.cs", Registrations.Replace("AddSingleton", "AddScoped"));
        var (warm, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeTrue("only a file's text changed, and the compilation before it is kept");
        CompilerErrors(warm).Should().BeEmpty();
        warm.Module("Welcome").Should().Contain("$eq.services.provide('IGreeting', () => new Greeting(), 'scoped')",
            "the registrations are read from the compilation the warm build kept, with the saved file in it");
        ModulesOf(warm).Should().Equal(ModulesOf(solution.Build()),
            "a warm build writes what a cold build writes after the same edit");
    }

    [Fact]
    public void AFileAddedSinceTheLastBuild_LoadsCold()
    {
        using var solution = Shop();
        var cold = solution.Build();

        solution.Write("Shop", "Components/Badge.cs", Tag.Replace("class Tag", "class Badge"));
        var (warm, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeFalse("a file a cold load reads that the kept compilation does not hold");
        CompilerErrors(warm).Should().BeEmpty();
        warm.Modules.Should().Contain("Badge");
    }

    /// <summary>
    /// A file deleted from a referenced project is a change of that project too: its tree is in the kept
    /// compilation and its file is not on disk, which a check that read only the files on disk passed
    /// over (found by the secondary review of #1346).
    /// </summary>
    [Fact]
    public void AFileOfAReferencedProjectThatWasRemoved_LoadsCold()
    {
        using var solution = new Solution()
            .Library("Common", globalUsings: SharedLibrary.Factories, files:
            [
                ("Prices.cs", Prices.Replace("namespace Shop", "namespace Common")),
                ("Extra.cs", "namespace Common;\n\npublic static class Extra { public const int Count = 1; }\n"),
            ])
            .App("Shop", references: ["Common"], globalUsings: SharedLibrary.Factories,
                files: [("Components/Tag.cs", Tag.Replace("namespace Shop;", "using Common;\n\nnamespace Shop;"))]);
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();

        File.Delete(solution.FileOf("Common", "Extra.cs"));
        var (_, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeFalse("the kept compilation holds a file the project no longer has");
    }

    [Fact]
    public void AFileOfAReferencedProjectThatChanged_LoadsCold()
    {
        using var solution = new Solution()
            .Library("Common", globalUsings: SharedLibrary.Factories, files: [("Prices.cs", Prices.Replace("namespace Shop", "namespace Common"))])
            .App("Shop", references: ["Common"], globalUsings: SharedLibrary.Factories,
                files: [("Components/Tag.cs", Tag.Replace("namespace Shop;", "using Common;\n\nnamespace Shop;"))]);
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();

        solution.Write("Common", "Prices.cs", Prices.Replace("namespace Shop", "namespace Common").Replace("Ten coins", "Eleven coins"));
        var (warm, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeFalse("a referenced project's compilation is not kept across its own edits");
        warm.Module("Tag").Should().Contain("Eleven coins");
    }

    /// <summary>
    /// A file an editor's save holds for a moment, between the look and the read, sends the build to a
    /// cold load: the served eqc that asked for a warm one is never taken down by it.
    /// </summary>
    [Fact]
    public void AFileHeldWhileTheBuildReadsIt_LoadsCold_AndThrowsNothing()
    {
        using var solution = Shop();
        var cold = solution.Build();
        var held = solution.FileOf("Shop", "Prices.cs");

        bool wasWarm;
        using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var rebuild = () => solution.Rebuild(cold);
            wasWarm = rebuild.Should().NotThrow().Subject.Warm;
        }

        wasWarm.Should().BeFalse("a file it could not read is a cold load's to read");
    }

    /// <summary>
    /// An assembly the reference list names, built again under the same path, loads the compilation
    /// cold: the list did not change, and the kept compilation would read the assembly as it was.
    /// </summary>
    [Fact]
    public void AReferencedAssemblyBuiltAgain_LoadsCold()
    {
        using var solution = new Solution()
            .Package("Pack", ("Brand.cs", "namespace Pack;\n\npublic static class Brand { public const string Name = \"Pack\"; }\n"))
            .App("Shop", references: ["Pack"], globalUsings: SharedLibrary.Factories, files: [("Prices.cs", Prices), ("Components/Tag.cs", Tag)]);
        var cold = solution.Build();
        var assembly = solution.AssemblyOf("Pack");
        File.SetLastWriteTimeUtc(assembly, DateTime.UtcNow.AddMinutes(1));

        var (_, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeFalse("an assembly the compilation read changed on disk");
    }

    /// <summary>
    /// A file a save changed is parsed with the symbols its build compiled it with (#1401), as a cold
    /// load parses it: parsed with none, the warm build took the branch the build did not, and wrote what
    /// no cold build writes.
    /// </summary>
    [Fact]
    public void AChangedFile_IsParsedWithTheSymbolsItsBuildCompiledItWith()
    {
        const string edition =
            """
            namespace Shop;

            public static class Edition
            {
                public static string Name()
                {
            #if DEBUG
                    return "debug";
            #else
                    return "release";
            #endif
                }
            }
            """;
        const string badge =
            """
            using eQuantic.UI.Primitives;

            namespace Shop;

            public sealed class Badge : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Text(Edition.Name(), TypeRole.BodyM);
            }
            """;
        using var solution = new Solution()
            .App("Shop", globalUsings: SharedLibrary.Factories, files: [("Edition.cs", edition), ("Components/Badge.cs", badge)])
            .Defines("Shop", "DEBUG;TRACE");
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();
        cold.Module("Edition").Should().Contain("'debug'");

        solution.Write("Shop", "Edition.cs", edition.Replace("\"debug\"", "\"debugging\""));
        var (warm, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeTrue("only a file's text changed");
        CompilerErrors(warm).Should().BeEmpty();
        warm.Module("Edition").Should().Contain("'debugging'", "the project defines DEBUG, and the build compiled that branch")
            .And.NotContain("'release'");
        ModulesOf(warm).Should().Equal(ModulesOf(solution.Build()), "a warm build writes what a cold build writes");
    }
}
