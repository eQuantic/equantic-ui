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
}
