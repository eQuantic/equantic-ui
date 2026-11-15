using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A compile writes a module only when its text changed (#1279). It rewrote every module on every
/// compile, so every file's time moved on every save, which bun and anything that goes by a file's
/// time took for a change.
/// </summary>
public class AModuleIsWrittenWhenItChangedTests
{
    private const string Prices =
        """
        namespace Shop;

        public static class Prices
        {
            public const string Label = "Ten coins";
        }
        """;

    private const string Tag =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Tag : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => Text(Prices.Label, TypeRole.BodyM);
        }
        """;

    private const string Badge =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Badge : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => Text("New", TypeRole.BodyM);
        }
        """;

    [Fact]
    public void ASaveThatChangesOneModule_WritesThatModule_AndNoOther()
    {
        using var solution = new Solution()
            .App("Shop", globalUsings: SharedLibrary.Factories,
                files: [("Prices.cs", Prices), ("Components/Tag.cs", Tag), ("Components/Badge.cs", Badge)]);
        var first = solution.Build();
        first.Modules.Should().Contain(["Tag", "Badge"]);
        // Every module an hour old, so a module written again by the next compile is one whose time moved.
        var long_ago = DateTime.UtcNow.AddHours(-1);
        foreach (var name in first.Modules) File.SetLastWriteTimeUtc(first.ModulePath(name), long_ago);

        solution.Write("Shop", "Prices.cs", Prices.Replace("Ten coins", "Eleven coins"));
        var (second, _) = solution.Rebuild(first);

        File.GetLastWriteTimeUtc(second.ModulePath("Tag")).Should().BeAfter(long_ago,
            "its text changed, since it inlines the constant the save changed");
        second.Module("Tag").Should().Contain("Eleven coins");
        File.GetLastWriteTimeUtc(second.ModulePath("Badge")).Should().Be(long_ago,
            "its text is what the file holds already, so it is not written again");
    }

    /// <summary>
    /// A catalogue is written only when its text changed, and one this compile did not produce leaves:
    /// a culture taken out leaves no catalogue behind. By the catalogue's name, never by its time,
    /// which the SDK's prune went by until eqc stopped writing what did not change (#1279).
    /// </summary>
    [Fact]
    public void ACultureTakenOut_LeavesNoCatalogue_AndAnUnchangedOneIsNotWrittenAgain()
    {
        using var solution = new Solution()
            .App("Shop", globalUsings: SharedLibrary.Factories, files:
            [
                ("Components/Title.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Shop.Resources;

                    namespace Shop;

                    public sealed class Title : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) => Text(Strings.Title, TypeRole.BodyM);
                    }
                    """),
                ("Resources/Strings.Designer.cs", SharedLibrary.Designer("Shop.Resources", "Title")),
                ("Resources/Strings.resx", SharedLibrary.Resx(("Title", "Shop"))),
                ("Resources/Strings.fr.resx", SharedLibrary.Resx(("Title", "Boutique"))),
            ]);
        var first = solution.Build();
        var strings = Path.Combine(first.OutputDirectory, "strings");
        File.Exists(Path.Combine(strings, "fr.json")).Should().BeTrue();
        var long_ago = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(Path.Combine(strings, "neutral.json"), long_ago);

        File.Delete(solution.FileOf("Shop", "Resources/Strings.fr.resx"));
        var (second, _) = solution.Rebuild(first);

        File.Exists(Path.Combine(strings, "fr.json")).Should().BeFalse("the culture was taken out, and this compile did not produce its catalogue");
        second.Catalog("neutral").Should().Contain("Strings/Title", "Shop");
        File.GetLastWriteTimeUtc(Path.Combine(strings, "neutral.json")).Should().Be(long_ago,
            "its text is what the file holds already, so it is not written again");
    }
}
