using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A QUALIFIED type name in a signature (<c>global::Ns.Thing</c>, <c>System.Action&lt;T&gt;</c>)
/// must reach the TypeScript annotation as the simple name a module actually binds. Qualification
/// is how generated code avoids ambiguity, so a SOURCE GENERATOR writes it by default — and echoed
/// verbatim it is not merely ugly: <c>global::</c> is a syntax error, and the BUNDLER dies on it
/// with "Expected \")\" but found \":\"", a long way from the signature that caused it.
/// </summary>
public class QualifiedTypeNameTests
{
    private static CompilationResult Compile(string body)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Holder : StatelessComponent
            {
            {{body}}

                public override VisualNode Build(ComponentContext context) =>
                    new Text("x", TypeRole.BodyM);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, path: "Holder.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Q", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Holder.cs").Single();
    }

    [Fact]
    public void GlobalAlias_NeverSurvivesIntoTypeScript()
    {
        var result = Compile("""
                public static global::eQuantic.UI.Primitives.VisualNode Wrap(
                    global::eQuantic.UI.Primitives.VisualNode child) => child;
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.DoesNotContain("global::", result.TypeScript);
        Assert.Contains("child: VisualNode", result.TypeScript);
    }

    [Fact]
    public void NamespaceQualification_ReducesToTheBindableName()
    {
        var result = Compile("""
                public static VisualNode Wrap(eQuantic.UI.Primitives.VisualNode child) => child;
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("child: VisualNode", result.TypeScript);
        Assert.DoesNotContain("eQuantic.UI.Primitives.VisualNode", result.TypeScript);
    }

    [Fact]
    public void AQualifiedGeneric_StillMapsThroughTheNameBasedRules()
    {
        // The List/Dictionary/Action mappings only ever matched UNQUALIFIED spellings, so this is
        // also what makes a fully-qualified BCL type map at all instead of echoing through.
        var result = Compile("""
                public static int Count(System.Collections.Generic.List<string> items) => items.Count;
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("items: string[]", result.TypeScript);
        Assert.DoesNotContain("System.Collections", result.TypeScript);
    }

    [Fact]
    public void AQualifiedEnum_KeepsItsStringRepresentation()
    {
        // The enum rule lives behind a comparison with the declared spelling; normalising only the
        // mapper's input would have made every qualified enum skip it and annotate as a class name.
        var result = Compile("""
                public static string Name(global::eQuantic.UI.Primitives.TypeRole role) => "x";
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("role: string", result.TypeScript);
        Assert.DoesNotContain("role: TypeRole", result.TypeScript);
    }
}
