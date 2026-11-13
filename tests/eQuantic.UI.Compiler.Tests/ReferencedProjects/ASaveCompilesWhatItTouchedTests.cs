using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A served eqc compiles again only the files a save can have changed (#1281): after a save that changed
/// bodies alone, the files it changed and the files whose last compile read one of them, and after any
/// other save, every file. Every case holds the warm build to what a cold build writes after the same
/// edit, byte for byte, and the cases whose dependency is a read first prove that the read matters: the
/// module a cold build writes changes with the edit, so a build that forgot the read would leave it stale.
/// </summary>
public class ASaveCompilesWhatItTouchedTests
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

    private const string Banner =
        """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class Banner : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => Text("Welcome", TypeRole.BodyM);
        }
        """;

    private static Solution Shop(params (string Path, string Source)[] more) => new Solution()
        .App("Shop", globalUsings: SharedLibrary.Factories,
            files: [("Prices.cs", Prices), ("Components/Tag.cs", Tag), ("Components/Banner.cs", Banner), .. more]);

    private static string CompilerErrors(Built built) =>
        string.Join('\n', built.Errors.Split('\n').Where(line => line.Contains(": error ", StringComparison.Ordinal)));

    private static Dictionary<string, string> ModulesOf(Built built) =>
        built.Modules.ToDictionary(name => name, name => built.Module(name)!);

    private static IEnumerable<string> Names(IEnumerable<string> files) => files.Select(Path.GetFileName)!;

    /// <summary>The warm build after <paramref name="edit"/>, and the cold one after it, which it must equal.</summary>
    private static (Built Warm, Dictionary<string, string> WarmModules, Dictionary<string, string> Cold) Save(
        Solution solution, Built before, Action edit)
    {
        edit();
        var (warm, wasWarm) = solution.Rebuild(before);
        wasWarm.Should().BeTrue("only the text of a file changed");
        CompilerErrors(warm).Should().BeEmpty();
        var warmModules = ModulesOf(warm);
        return (warm, warmModules, ModulesOf(solution.Build()));
    }

    [Fact]
    public void ABodyOfOneScreen_CompilesThatFileAlone()
    {
        using var solution = Shop();
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();

        var (warm, modules, coldAgain) = Save(solution, cold,
            () => solution.Write("Shop", "Components/Banner.cs", Banner.Replace("Welcome", "Hello there")));

        Names(warm.AppBuild.CompiledFiles).Should().Equal(["Banner.cs"],
            "the save changed a body of Banner.cs, and no other file's compile read it");
        modules["Banner"].Should().Contain("Hello there");
        modules.Should().Equal(coldAgain, "a warm build writes what a cold build writes after the same edit");
        warm.Output.Should().Contain("Compiled 1 of 3 file(s)");
    }

    [Fact]
    public void ADefaultInterfaceMembersBody_CompilesTheClassItIsCopiedInto()
    {
        const string greeter = "namespace Shop;\n\npublic interface IGreeter\n{\n    string Greeting() => \"Hello\";\n}\n";
        const string welcome = "namespace Shop;\n\npublic sealed class Welcome : IGreeter\n{\n}\n";
        const string card =
            """
            using eQuantic.UI.Primitives;

            namespace Shop;

            public sealed class Card : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Text(((IGreeter)new Welcome()).Greeting(), TypeRole.BodyM);
            }
            """;
        using var solution = Shop(("IGreeter.cs", greeter), ("Welcome.cs", welcome), ("Components/Card.cs", card));
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();
        var before = ModulesOf(cold);

        var (warm, modules, coldAgain) = Save(solution, cold,
            () => solution.Write("Shop", "IGreeter.cs", greeter.Replace("Hello", "Hola")));

        coldAgain["Welcome"].Should().NotBe(before["Welcome"], "the default member's body is copied into the class's module");
        Names(warm.AppBuild.CompiledFiles).Should().Contain("Welcome.cs", "its compile read the interface's file");
        Names(warm.AppBuild.CompiledFiles).Should().NotContain("Banner.cs");
        modules.Should().Equal(coldAgain);
    }

    /// <summary>
    /// Two files call the method, and whether it writes its value is read once and kept: the second
    /// compile takes the answer the first one computed, and must note the body the first one read.
    /// </summary>
    [Fact]
    public void AStructMethodsBody_CompilesEveryFileThatCallsIt()
    {
        const string counter =
            "namespace Shop;\n\npublic struct Counter\n{\n    public int Count;\n\n    public void Bump() { Count++; }\n}\n";
        const string tally =
            """
            using eQuantic.UI.Primitives;

            namespace Shop;

            public sealed class Tally : StatelessComponent
            {
                private readonly Counter _counter = new();

                public override VisualNode Build(ComponentContext context)
                {
                    _counter.Bump();
                    return Text($"{_counter.Count}", TypeRole.BodyM);
                }
            }
            """;
        using var solution = Shop(("Counter.cs", counter), ("Components/Tally.cs", tally),
            ("Components/Recount.cs", tally.Replace("class Tally", "class Recount")));
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();
        var before = ModulesOf(cold);

        var (warm, modules, coldAgain) = Save(solution, cold,
            () => solution.Write("Shop", "Counter.cs", counter.Replace("{ Count++; }", "{ }")));

        coldAgain["Tally"].Should().NotBe(before["Tally"],
            "a call on a readonly field copies the value only when the method writes it, which its body says");
        coldAgain["Recount"].Should().NotBe(before["Recount"]);
        Names(warm.AppBuild.CompiledFiles).Should().Contain(["Tally.cs", "Recount.cs"],
            "each compile read the method's body, the second through the answer the first one kept");
        modules.Should().Equal(coldAgain);
    }

    [Fact]
    public void AConstant_CompilesEveryFile()
    {
        using var solution = Shop();
        var cold = solution.Build();

        var (warm, modules, coldAgain) = Save(solution, cold,
            () => solution.Write("Shop", "Prices.cs", Prices.Replace("Ten coins", "Eleven coins")));

        warm.AppBuild.CompiledFiles.Should().HaveCount(3, "a declaration changed, which another file binds to");
        warm.Output.Should().Contain("A declaration changed in");
        modules["Tag"].Should().Contain("Eleven coins");
        modules.Should().Equal(coldAgain);
    }

    [Fact]
    public void AConstructorAFactoryCalls_CompilesEveryFile()
    {
        const string factories =
            "namespace Shop;\n\npublic static class ShopUI\n{\n    public static Badge Badge(string label) => new(label);\n}\n";
        const string badge =
            """
            using eQuantic.UI.Primitives;

            namespace Shop;

            public sealed class Badge(string label) : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Text(label, TypeRole.BodyM);
            }
            """;
        const string shelf =
            """
            using eQuantic.UI.Primitives;

            namespace Shop;

            public sealed class Shelf : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => ShopUI.Badge("New");
            }
            """;
        using var solution = Shop(("ShopUI.cs", factories), ("Components/Badge.cs", badge), ("Components/Shelf.cs", shelf));
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();

        var (warm, modules, coldAgain) = Save(solution, cold, () => solution.Write("Shop", "Components/Badge.cs",
            badge.Replace("Badge(string label)", "Badge(string label, string suffix = \"!\")").Replace("Text(label", "Text(label + suffix")));

        warm.AppBuild.CompiledFiles.Should().HaveCount(6, "the constructor is a declaration the factory binds to");
        modules.Should().Equal(coldAgain);
    }

    [Fact]
    public void AFileNotCompiledAgain_StillHandsTheCatalogsItsStrings()
    {
        const string title =
            """
            using eQuantic.UI.Primitives;
            using Shop.Resources;

            namespace Shop;

            public sealed class Title : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Text(Strings.Heading, TypeRole.BodyM);
            }
            """;
        using var solution = Shop(
            ("Components/Title.cs", title),
            ("Resources/Strings.Designer.cs", SharedLibrary.Designer("Shop.Resources", "Heading")),
            ("Resources/Strings.resx", SharedLibrary.Resx(("Heading", "The shop"))));
        var cold = solution.Build();
        CompilerErrors(cold).Should().BeEmpty();
        cold.Catalog("neutral").Should().Contain("Strings/Heading", "The shop");

        // Read before the cold build that follows, which writes the catalogs again into the same folder.
        solution.Write("Shop", "Components/Banner.cs", Banner.Replace("Welcome", "Hello there"));
        var (warm, wasWarm) = solution.Rebuild(cold);
        var catalog = warm.Catalog("neutral");

        wasWarm.Should().BeTrue();
        Names(warm.AppBuild.CompiledFiles).Should().NotContain("Title.cs");
        catalog.Should().Contain("Strings/Heading", "The shop",
            "a file taken as the last build compiled it still hands the catalogs the strings it reads");
    }

    [Fact]
    public void AFileWhoseLastCompileFailed_IsCompiledAgain()
    {
        const string broken =
            """
            using eQuantic.UI.Primitives;

            namespace Shop;

            public sealed class Broken : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Text(System.IO.File.ReadAllText("motd.txt"), TypeRole.BodyM);
            }
            """;
        using var solution = Shop(("Components/Broken.cs", broken));
        var cold = solution.Build();
        CompilerErrors(cold).Should().NotBeEmpty("a component reads a file, which never reaches the browser");

        solution.Write("Shop", "Components/Banner.cs", Banner.Replace("Welcome", "Hello there"));
        var (warm, wasWarm) = solution.Rebuild(cold);

        wasWarm.Should().BeTrue();
        Names(warm.AppBuild.CompiledFiles).Should().BeEquivalentTo(["Banner.cs", "Broken.cs"],
            "a file that failed says so again, from its compile, not from one the build kept");
        CompilerErrors(warm).Should().Contain("Broken.cs");
    }
}
