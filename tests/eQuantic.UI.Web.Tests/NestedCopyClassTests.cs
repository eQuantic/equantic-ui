using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The `private static class Copy` every section of a real site keeps its strings in.
///
/// <para>
/// It is emitted INLINE, above the component: as its own module, two same-named nested classes
/// would overwrite each other's file, and C# scoping is lexical anyway. What that inlining has to
/// carry with it is everything the nested body needs — and it did not.
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
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

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

        section.Should().Contain("$eq.str(\"Strings\", \"About\")");
        section.Should().MatchRegex(@"import \{[^}]*\$eq[^}]*\} from ""@equantic/runtime""",
            "a module that says $eq has to import it, or it fails to load whole");
    }

    /// <summary>
    /// And it must not import ITSELF. The nested class is emitted in this module, so a
    /// `from "./Copy"` names a module that was never written — which is a load failure on any path
    /// that does not bundle the import away.
    /// </summary>
    [Fact]
    public void ANestedClass_IsNotImportedFromAModuleThatDoesNotExist()
    {
        var section = Transpile(Localized);

        section.Should().Contain("class Copy", "the nested class is emitted inline, by design");
        section.Should().NotContain("from \"./Copy\"",
            "there is no such module — the class is right here");
    }
}
