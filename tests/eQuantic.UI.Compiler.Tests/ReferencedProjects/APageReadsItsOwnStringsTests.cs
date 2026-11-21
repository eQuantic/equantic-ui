using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A page ships the catalogue strings its modules read (#1782): eqc writes, beside the catalogues, which
/// strings each page reads through the modules it reaches (<c>page-strings.json</c>), and a catalogue holds
/// only what the modules the browser reaches read. A real site's shop page carried its back office's
/// strings, and a string only server code read reached every catalogue.
/// </summary>
public class APageReadsItsOwnStringsTests
{
    private static Solution Shop() => new Solution()
        .App("Shop", globalUsings: SharedLibrary.Factories, files:
        [
            ("Pages/HomePage.cs",
                """
                using eQuantic.UI.Primitives;
                using Shop.Resources;

                namespace Shop.Pages;

                [Page("/")]
                public sealed class HomePage : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) =>
                        Column(gap: 8, children: [Text(Strings.Cakes, TypeRole.Heading), new Shop.Badge()]);
                }
                """),
            ("Pages/AdminPage.cs",
                """
                using eQuantic.UI.Primitives;
                using Shop.Resources;

                namespace Shop.Pages;

                [Page("/admin")]
                public sealed class AdminPage : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) => Text(Strings.Orders, TypeRole.Heading);
                }
                """),
            ("Components/Badge.cs",
                """
                using eQuantic.UI.Primitives;
                using Shop.Resources;

                namespace Shop;

                public sealed class Badge : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) => Text(Strings.Fresh, TypeRole.Label);
                }
                """),
            ("Services/Mailer.cs",
                """
                using Shop.Resources;

                namespace Shop.Services;

                public sealed class Mailer
                {
                    public string Subject() => Strings.Receipt;
                }
                """),
            ("Resources/Strings.Designer.cs", SharedLibrary.Designer("Shop.Resources", "Cakes", "Orders", "Fresh", "Receipt")),
            ("Resources/Strings.resx", SharedLibrary.Resx(("Cakes", "Our cakes"), ("Orders", "Orders to ship"),
                ("Fresh", "Fresh today"), ("Receipt", "Your receipt"))),
        ]);

    /// <summary>The build wrote its modules, its catalogues and its index without a diagnostic: a page is an
    /// entry point of the bundle, and the bundling this harness does not run is the only thing left out.</summary>
    private static void TranspiledClean(Built built) =>
        built.Errors.Split('\n').Where(line => line.Length > 0 && !line.Contains("Embedded Bun not found"))
            .Should().BeEmpty();

    [Fact]
    public void EachPage_IsIndexedWithTheStringsItsModulesRead()
    {
        using var solution = Shop();
        var built = solution.Build();
        TranspiledClean(built);

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(built.OutputDirectory, "page-strings.json")));
        var pages = index.RootElement.GetProperty("pages");
        pages.GetProperty("HomePage").EnumerateArray().Select(key => key.GetString()).Should()
            .Equal(["Strings/Cakes", "Strings/Fresh"], "the home page reads one string, and the badge it places another");
        pages.GetProperty("AdminPage").EnumerateArray().Select(key => key.GetString()).Should()
            .Equal(["Strings/Orders"], "the back office's string is its own page's alone");
    }

    /// <summary>
    /// The factory surface imports every component of the app, so a page that composes one through it
    /// reaches the component that factory builds and no other: followed as an import, it would hand every
    /// page every string.
    /// </summary>
    [Fact]
    public void APageComposingThroughTheFactorySurface_ReadsOnlyTheComponentsItBuilds()
    {
        using var solution = Shop()
            .Write("Shop", "Components/Report.cs",
                """
                using eQuantic.UI.Primitives;
                using Shop.Resources;

                namespace Shop;

                public sealed class Report : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) => Text(Strings.Orders, TypeRole.Label);
                }
                """)
            .Write("Shop", "Components/AppUI.cs",
                """
                namespace Shop;

                public static class AppUI
                {
                    public static Badge Badge() => new Badge();
                    public static Report Report() => new Report();
                }
                """)
            .Write("Shop", "Pages/HomePage.cs",
                """
                using eQuantic.UI.Primitives;
                using Shop.Resources;

                namespace Shop.Pages;

                [Page("/")]
                public sealed class HomePage : StatelessComponent
                {
                    public override VisualNode Build(ComponentContext context) =>
                        Column(gap: 8, children: [Text(Strings.Cakes, TypeRole.Heading), Shop.AppUI.Badge()]);
                }
                """);
        var built = solution.Build();
        TranspiledClean(built);

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(built.OutputDirectory, "page-strings.json")));
        index.RootElement.GetProperty("pages").GetProperty("HomePage").EnumerateArray().Select(key => key.GetString())
            .Should().Equal(["Strings/Cakes", "Strings/Fresh"], "the report is built by a factory this page never calls");
    }

    [Fact]
    public void AStringOnlyServerCodeReads_StaysOutOfTheCatalogue()
    {
        using var solution = Shop();
        var built = solution.Build();
        TranspiledClean(built);

        built.Catalog("neutral").Keys.Should().BeEquivalentTo(["Strings/Cakes", "Strings/Orders", "Strings/Fresh"],
            "the mailer is server code no component names, so the browser never reads its string");
    }
}
