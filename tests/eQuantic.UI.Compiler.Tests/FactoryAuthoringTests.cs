using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The declarative factory surface through eqc: a page authored with
/// <c>using static eQuantic.UI.Components.UI;</c> and ZERO <c>new</c> keywords must emit
/// class-qualified twin calls (<c>UI.column(…)</c>) with the <c>UI</c> import riding the runtime —
/// in BOTH modes. With references the semantic path resolves the statics; standalone (the
/// playground) the `using static` directive itself is the evidence.
/// </summary>
public class FactoryAuthoringTests
{
    private const string FactoryPage = """
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;

        namespace Demo;

        public sealed class Decl : StatefulComponent
        {
            private int _count;

            public override VisualNode Build(ComponentContext context) =>
                Column(gap: Space.S3, children: [
                    Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
                    Button("Up", onPressed: () => SetState(() => _count++)),
                ]);
        }
        """;

    private static CompilationResult CompileWithRefs(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Decl.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Components.Button).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Decl.cs").Single();
    }

    [Fact]
    public void WithRefs_FactoriesEmitClassQualifiedTwinCalls()
    {
        var result = CompileWithRefs(FactoryPage);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        // The named `gap:` argument lands in slot 0, the children collection in slot 1 — and the
        // Button named argument fills the skipped variant/size slots from their defaults.
        Assert.Contains("UI.column(12, [UI.text(", result.TypeScript);
        Assert.Contains("UI.button('Up', 'primary', 'medium', () =>", result.TypeScript);
        Assert.DoesNotContain("new Column", result.TypeScript);
        Assert.DoesNotContain("new Text", result.TypeScript);
        Assert.DoesNotContain("new Button", result.TypeScript);
        // The class the emission introduced is imported from the runtime.
        var imports = result.TypeScript.Split('\n').Where(l => l.StartsWith("import")).ToArray();
        Assert.Contains(imports, l => l.Contains("\"@equantic/runtime\"") && l.Contains("UI"));
    }

    [Fact]
    public void Standalone_TheUsingStaticDirectiveIsTheEvidence()
    {
        var result = new ComponentCompiler().CompileSource(FactoryPage, "Decl.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("UI.column(", result.TypeScript);
        Assert.Contains("UI.text(", result.TypeScript);
        Assert.Contains("UI.button(", result.TypeScript);
        Assert.DoesNotContain("this.column(", result.TypeScript);
        var imports = result.TypeScript.Split('\n').Where(l => l.StartsWith("import")).ToArray();
        Assert.Contains(imports, l => l.Contains("\"@equantic/runtime\"") && l.Contains("UI"));
    }

    [Fact]
    public void Standalone_AnOwnMethodNamedLikeAFactory_StaysTheirs()
    {
        var result = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Decl : StatelessComponent
            {
                private VisualNode Header() => Text("mine", TypeRole.Title);

                public override VisualNode Build(ComponentContext context) =>
                    Column(gap: 4, children: [Header()]);
            }
            """, "Decl.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        // The class's own member keeps the `this.` path; only unclaimed names go to the factory.
        Assert.Contains("this.header()", result.TypeScript);
        Assert.DoesNotContain("UI.header(", result.TypeScript);
    }

    [Fact]
    public void Standalone_WithoutTheDirective_NothingChanges()
    {
        var result = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Decl : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text("plain", TypeRole.BodyM);
            }
            """, "Decl.cs").Single();

        Assert.True(result.Success);
        Assert.DoesNotContain("UI.", result.TypeScript);
    }

    [Fact]
    public void WithRefs_NamedInPositionThenPositionalChildren_DoNotClobberSlotZero()
    {
        // `Column(gap: Space.S3, [ … ])` — the named argument sits IN position 0 and the children
        // array follows positionally. The old reorder restarted its positional counter at 0 and the
        // array overwrote the gap.
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Decl : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Column(gap: Space.S3, [Text("x", TypeRole.BodyM)]);
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("UI.column(12, [UI.text(", result.TypeScript);
    }
}
