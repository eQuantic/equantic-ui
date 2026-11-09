using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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

    /// <summary>
    /// A runtime's folder on Windows, where its native libraries are <c>.dll</c> beside its assemblies:
    /// only the assemblies are referenced. Every native one was CS0009 and the icon failed to build on
    /// Windows alone, where macOS and Linux name theirs <c>.dylib</c> and <c>.so</c>.
    /// </summary>
    [Fact]
    public void AFoldersNativeLibraries_AreNotReferenced_OnlyItsAssemblies()
    {
        var folder = Directory.CreateTempSubdirectory("eq-icon-refs-").FullName;
        try
        {
            File.Copy(typeof(FluentAssertions.AssertionExtensions).Assembly.Location, Path.Combine(folder, "Managed.dll"));
            NativeLibrary.WriteTo(Path.Combine(folder, "coreclr.dll"));
            File.WriteAllBytes(Path.Combine(folder, "garbage.dll"), [1, 2, 3, 4]);

            var references = IconCompilation.ReferencesIn([folder]);

            references.Select(reference => Path.GetFileName(reference.Display)).Should().Equal("Managed.dll");
            CSharpCompilation.Create("probe", [], references).GetDiagnostics()
                .Should().NotContain(diagnostic => diagnostic.Id == "CS0009");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A PE image with code and no metadata, as a native library is.</summary>
    private sealed class NativeLibrary() : PEBuilder(PEHeaderBuilder.CreateLibraryHeader(), deterministicIdProvider: null)
    {
        public static void WriteTo(string path)
        {
            var blob = new BlobBuilder();
            new NativeLibrary().Serialize(blob);
            File.WriteAllBytes(path, blob.ToArray());
        }

        protected override PEDirectoriesBuilder GetDirectories() => new();

        protected override ImmutableArray<Section> CreateSections() =>
            [new Section(".text", SectionCharacteristics.ContainsCode | SectionCharacteristics.MemRead | SectionCharacteristics.MemExecute)];

        protected override BlobBuilder SerializeSection(string name, SectionLocation location)
        {
            var section = new BlobBuilder();
            section.WriteBytes(0xC3, 16);
            return section;
        }
    }

    [Fact]
    public void WithoutTheCatalog_AVectorIsNotThere()
    {
        var compilation = IconCompilation.Create([Tree(Icon, "AppIcon.cs")], ["static eQuantic.UI.Components.UI"], References);

        Errors(compilation).Select(diagnostic => diagnostic.Id).Should().Contain("CS0103");
    }
}
