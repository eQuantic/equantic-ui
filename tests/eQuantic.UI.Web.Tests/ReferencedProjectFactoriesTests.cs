using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A component of a referenced project is composed like the app's own: <c>Wordmark()</c>, with no setting
/// and no attribute (#818). The generator sees a referenced project only as metadata, and the SDK names the
/// assemblies that came from projects (<c>_EqReferencedProjects</c>): a package's components have no source
/// eqc compiles, so no module a factory could import, and get none. <c>samples/SharedLibrary</c> composes
/// its library's <c>Wordmark</c> this way.
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

    private sealed class Options(string? referenced) : AnalyzerConfigOptionsProvider
    {
        private sealed class Global(string? referenced) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = key == "build_property._EqReferencedProjects" ? referenced : null;
                return value is not null;
            }
        }

        public override AnalyzerConfigOptions GlobalOptions { get; } = new Global(referenced);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
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

    private static (string Source, ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<Diagnostic> Errors) Run(
        string app, string? referenced, string assemblyName = "Shop.Web")
    {
        var compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(app, path: "App.cs")],
            Framework().Append(CompiledLibrary()),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
                [new AppFactorySurfaceGenerator().AsSourceGenerator()], optionsProvider: new Options(referenced))
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
