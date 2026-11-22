using System.Collections.Immutable;
using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A component of a referenced project is composed like the app's own: <c>Wordmark()</c>, with no setting
/// and no attribute (#818). The generator sees a referenced project only as metadata, and the SDK names the
/// assemblies that came from projects, one a line, in a file it hands the compiler
/// (<c>equantic.factory-projects.txt</c>): a package's components have no source eqc compiles, so no module
/// a factory could import, and get none. <c>samples/SharedLibrary</c> composes its library's <c>Wordmark</c>
/// this way, and <see cref="ReferencedProjectFactoriesBuildTests"/> builds the same through MSBuild and the
/// compiler, which is where a list handed in an analyzer config value lost all but its first name (#1803).
/// </summary>
public class ReferencedProjectFactoriesTests
{
    private const string Library = """
        using eQuantic.UI.Primitives;

        namespace Brand.Common;

        public sealed class Wordmark : StatelessComponent
        {
            public Wordmark(string tagline = "") { Tagline = tagline; }
            public string Tagline { get; }
            public override VisualNode Build(ComponentContext context) => new Text(Tagline);
        }

        internal sealed class Hidden : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text("");
        }
        """;

    /// <summary>A file the compiler is handed with the additional files, read from memory.</summary>
    private sealed class Listed(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }

    private static IEnumerable<MetadataReference> Framework() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.PageAttribute).Assembly.Location));

    /// <summary>The library, compiled as the app's reference sees it: an assembly named Brand.Common.</summary>
    private static MetadataReference CompiledLibrary()
    {
        var library = CSharpCompilation.Create("Brand.Common", [CSharpSyntaxTree.ParseText(Library)], Framework(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var image = new MemoryStream();
        var emitted = library.Emit(image);
        emitted.Success.Should().BeTrue(string.Join("\n", emitted.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    /// <summary>
    /// Runs the generator over the app, with <paramref name="referenced"/> as the lines of the list the SDK writes,
    /// in a file named <paramref name="listName"/>, or with no list at all when it is null.
    /// </summary>
    private static (string Source, ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<Diagnostic> Errors) Run(
        string app, string? referenced, string assemblyName = "Shop.Web", string listName = "equantic.factory-projects.txt")
    {
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(app, path: "App.cs")],
            Framework().Append(CompiledLibrary()),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        AdditionalText[] lists = referenced is null ? [] : [new Listed($"/obj/Debug/net10.0/{listName}", referenced)];
        var driver = CSharpGeneratorDriver.Create([new AppFactorySurfaceGenerator().AsSourceGenerator()], additionalTexts: lists)
            .RunGeneratorsAndUpdateCompilation(compilation, out var withSurface, out _);
        var result = driver.GetRunResult().Results.Single();
        var source = result.GeneratedSources.Length > 0 ? result.GeneratedSources[0].SourceText.ToString() : "";
        return (source, result.Diagnostics,
            withSurface.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList());
    }

    private const string PageThatComposesTheMark = """
        using eQuantic.UI.Primitives;

        namespace Shop.Web.Pages;

        [Page("/")]
        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => Wordmark("Since 1998");
        }
        """;

    [Fact]
    public void AReferencedProjectsComponent_GetsAFactory_AndThePageComposesIt()
    {
        var (source, diagnostics, errors) = Run(PageThatComposesTheMark, referenced: "Brand.Common");

        diagnostics.Should().BeEmpty();
        errors.Should().BeEmpty(source);
        source.Should().Contain("public static global::Brand.Common.Wordmark Wordmark(string tagline = \"\") => new global::Brand.Common.Wordmark(tagline);")
            .And.NotContain("Hidden", "an internal component of another assembly has no factory the app could call");
    }

    [Fact]
    public void AnAssemblyTheSdkDidNotName_GetsNoFactory()
    {
        // A package beside a project the SDK did name: the package's components have no source eqc
        // compiles, so a factory would import a module that does not exist.
        var (source, _, errors) = Run(PageThatComposesTheMark, referenced: "Brand.Theme");

        source.Should().NotContain("Wordmark");
        errors.Should().Contain(error => error.Id == "CS0103", "the page names a factory nobody wrote");
    }

    [Fact]
    public void AProjectAfterTheFirstInTheList_GetsItsFactories()
    {
        // The list a real site's app wrote: the project with the components is not the first (#1803).
        var (source, diagnostics, errors) = Run(PageThatComposesTheMark, referenced: "Brand.Application\nBrand.Domain\nBrand.Common\n");

        diagnostics.Should().BeEmpty();
        errors.Should().BeEmpty(source);
        source.Should().Contain("Wordmark(string tagline = \"\")");
    }

    [Fact]
    public void AFileOfAnotherName_IsNoList()
    {
        var (source, _, errors) = Run(PageThatComposesTheMark, referenced: "Brand.Common", listName: "projects.txt");

        source.Should().NotContain("Wordmark");
        errors.Should().Contain(error => error.Id == "CS0103", "only the SDK's own list names the projects");
    }

    [Fact]
    public void TheSurface_StaysInTheAppsNamespace_BesideAReferencedProjectsComponents()
    {
        var (source, _, errors) = Run("""
            using eQuantic.UI.Primitives;

            namespace Shop.Web.Components;

            public sealed class Basket : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Wordmark();
            }
            """, referenced: "Brand.Common");

        errors.Should().BeEmpty(source);
        source.Should().Contain("namespace Shop.Web.Components;", "a library's namespace does not narrow the app's surface")
            .And.Contain("Wordmark(");
    }

    [Fact]
    public void AnAppWithNoComponentsOfItsOwn_HasItsSurfaceInItsAssemblysNamespace()
    {
        var (source, _, errors) = Run(PageThatComposesTheMark, referenced: "Brand.Common", assemblyName: "Shop.Site");

        errors.Should().BeEmpty(source);
        source.Should().Contain("namespace Shop.Site;").And.Contain("global using static Shop.Site.AppUI;");
    }

    [Fact]
    public void TheAppsOwnComponent_KeepsTheName_AndTheReferencedOneIsReported()
    {
        var (source, diagnostics, errors) = Run("""
            using eQuantic.UI.Primitives;

            namespace Shop.Web.Components;

            public sealed class Wordmark : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text("ours");
            }
            """, referenced: "Brand.Common");

        errors.Should().BeEmpty(source);
        source.Should().Contain("public static global::Shop.Web.Components.Wordmark Wordmark()");
        diagnostics.Should().ContainSingle(d => d.Id == "EQ3102")
            .Which.GetMessage().Should().Contain("Brand.Common.Wordmark");
    }
}
