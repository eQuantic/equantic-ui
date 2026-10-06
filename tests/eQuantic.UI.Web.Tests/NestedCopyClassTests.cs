using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The `private static class Copy` every section of a real site keeps its strings in.
///
/// <para>
/// It is a module of its own, named by its section (`SiteFooter$Copy`, #584), so two sections'
/// `Copy` never write one file. It was emitted inline above the component, unexported, and what that
/// inlining had to carry with it, the helpers its body needs, it once did not.
/// </para>
/// </summary>
public class NestedCopyClassTests
{
    private static string Transpile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Section.cs");
        var usings = CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Linq;", path: "GlobalUsings.g.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path));

        var compilation = CSharpCompilation.Create("Nested", [tree, usings], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return string.Join("\n", compiler.CompileSource(source, "Section.cs")
            .Select(result => result.TypeScript));
    }

    /// <summary>A resx-backed string, read through the section's own Copy — the shape a localized
    /// site is written in.</summary>
    private const string Localized = """
        using System.Globalization;
        using System.Resources;
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace Fx;

        public static class Strings
        {
            private static ResourceManager? _manager;
            public static ResourceManager ResourceManager =>
                _manager ??= new ResourceManager("Fx.Strings", typeof(Strings).Assembly);
            public static CultureInfo? Culture { get; set; }
            public static string About => ResourceManager.GetString("About", Culture)!;
        }

        public sealed class SiteFooter : StatelessComponent
        {
            private static class Copy
            {
                public static string About => Strings.About;
            }

            public override VisualNode Build(ComponentContext context) =>
                new Text(Copy.About, TypeRole.BodyM);
        }
        """;

    /// <summary>
    /// The nested body registered `$eq.str(…)` and the module's import line had already been decided
    /// without it: the helpers were transferred from the converter BEFORE the nested classes were
    /// emitted. "$eq is not defined" fails the module whole, so the page did not render at all — and
    /// only in the browser, since the server runs the C#.
    /// </summary>
    [Fact]
    public void ANestedClassThatReadsAResource_BringsItsHelperImport()
    {
        var section = Transpile(Localized);

        section.Should().Contain("$eq.str('Strings', 'About')");
        section.Should().MatchRegex(@"import \{[^}]*\$eq[^}]*\} from ""@equantic/runtime""",
            "a module that says $eq has to import it, or it fails to load whole");
    }

    /// <summary>
    /// And it is imported from the module that is written. The section imports its `Copy` from
    /// `./SiteFooter$Copy`, the module named by its owner, never from a `./Copy` nobody writes, which
    /// is a load failure on any path that does not bundle the import away.
    /// </summary>
    [Fact]
    public void ANestedClass_IsImportedFromTheModuleNamedByItsOwner()
    {
        var section = Transpile(Localized);

        section.Should().Contain("export class SiteFooter$Copy", "the nested class is a module named by its owner (#584)");
        section.Should().Contain("import { SiteFooter$Copy } from \"./SiteFooter$Copy\"")
            .And.Contain("new Text(SiteFooter$Copy.about")
            .And.NotContain("from \"./Copy\"", "there is no such module");
    }
}
