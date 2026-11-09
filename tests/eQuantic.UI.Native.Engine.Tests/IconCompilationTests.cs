using eQuantic.UI.Build;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The compilation an app's icon is rasterized from (#1240): its file, beside the app's vector catalog,
/// with the usings the app compiles with. The icon is a file of the app and is written like one, and
/// before this it compiled alone, where <c>Box(...)</c> named the vocabulary's type instead of the
/// factory and <c>Vectors</c> did not exist.
/// </summary>
public class IconCompilationTests
{
    /// <summary>An icon written as the dashboard sample writes it: a factory, drawing the catalog's art.</summary>
    private const string Icon = """
        using eQuantic.UI.Primitives;

        namespace App;

        public sealed class AppIcon : IAppIcon
        {
            public VisualNode Build(ComponentContext context, AppIconUse use) =>
                Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill },
                    Drawing(Vectors.Mark, SizeValue.Fill, label: "App"));
        }
        """;

    /// <summary>A stand-in for the catalog the SDK writes from the app's SVGs: the same class, by name.</summary>
    private const string Catalog = """
        namespace App;

        public static class Vectors
        {
            public static eQuantic.UI.Primitives.VectorDrawing Mark { get; } = eQuantic.UI.Primitives.VectorDrawing.Empty;
        }
        """;

    private static readonly IReadOnlyList<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => TestReferences.Of(path))
            .ToList();

    private static SyntaxTree Tree(string source, string path) => CSharpSyntaxTree.ParseText(source, path: path);

    private static IEnumerable<Diagnostic> Errors(Compilation compilation) =>
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    [Fact]
    public void AnIconWrittenLikeAnyFileOfTheApp_Compiles_WithTheAppsUsingsAndItsCatalog()
    {
        var compilation = IconCompilation.Create(
            [Tree(Icon, "AppIcon.cs"), Tree(Catalog, "Vectors.g.cs")],
            ["System", "System.Linq", "static eQuantic.UI.Components.UI", "StatefulComponent = eQuantic.UI.Primitives.StatefulComponent"],
            References);

        Errors(compilation).Should().BeEmpty();
    }

    /// <summary>What the icon met before #1240, kept as the reason the usings are handed over.</summary>
    [Fact]
    public void WithoutTheAppsUsings_TheFactoryIsTheVocabularysType()
    {
        var compilation = IconCompilation.Create([Tree(Icon, "AppIcon.cs"), Tree(Catalog, "Vectors.g.cs")], [], References);

        Errors(compilation).Select(diagnostic => diagnostic.Id).Should().Contain("CS1955",
            "Box names the Primitives type, which cannot be invoked, where the app's global using static names the factory");
    }

    [Fact]
    public void AUsingTheVocabularyCannotResolve_IsDropped_AndTheRestKept()
    {
        // A web host's usings name ASP.NET namespaces the icon is not compiled against.
        var compilation = IconCompilation.Create(
            [Tree(Icon, "AppIcon.cs"), Tree(Catalog, "Vectors.g.cs")],
            ["Acme.Web.Hosting", "static eQuantic.UI.Components.UI", "static Acme.Web.Hosting.Endpoints"],
            References);

        Errors(compilation).Should().BeEmpty();
        var usings = compilation.SyntaxTrees.Single(tree => tree.FilePath.EndsWith("GlobalUsings.g.cs", StringComparison.Ordinal))
            .GetText().ToString();
        usings.Should().Contain("global using static eQuantic.UI.Components.UI;");
        usings.Should().NotContain("Acme");
    }

    [Fact]
    public void WithoutTheCatalog_AVectorIsNotThere()
    {
        var compilation = IconCompilation.Create([Tree(Icon, "AppIcon.cs")], ["static eQuantic.UI.Components.UI"], References);

        Errors(compilation).Select(diagnostic => diagnostic.Id).Should().Contain("CS0103");
    }
}
