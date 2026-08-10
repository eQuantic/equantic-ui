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
        System.Console.WriteLine(result.TypeScript);
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
        // `Text(content: "x", TypeRole.BodyM)` — the named argument sits IN position 0 and the next
        // one follows positionally. The old reorder restarted its positional counter at 0 and the
        // second argument overwrote the first.
        //
        // The Column around it is the other half: `children` is TRAILING, so naming it has to make
        // the emitter fill every knob between with its own default rather than shifting the array
        // into the first empty slot.
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Decl : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Column(gap: Space.S3, children: [Text(content: "x", TypeRole.BodyM)]);
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("UI.column(12, 'start', 'stretch', false, null, null, [UI.text('x', 'bodyM')])",
            result.TypeScript);
    }

    /// <summary>
    /// A page reading its ROUTE imports `RouteValues` from the runtime — the emission half of the
    /// promise the runtime's `primitives-exports.spec.ts` keeps.
    /// <para>
    /// eqc routes the whole Primitives namespace to `@equantic/runtime` implicitly, so declaring a
    /// public type there silently promises an export. `RouteValues` shipped without one and every
    /// page naming it died at hydration on "does not provide an export named 'RouteValues'" — while
    /// SSR kept answering 200 with correct markup, so nothing looked wrong until the screen was
    /// blank. This pins that the import really is emitted; the vitest spec pins that it resolves.
    /// </para>
    /// </summary>
    [Fact]
    public void WithRefs_APageReadingItsRoute_ImportsRouteValuesFromTheRuntime()
    {
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Routed : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Text(RouteValues.Current.Param("slug") ?? "none", TypeRole.BodyM);
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("RouteValues", result.TypeScript);
        Assert.Matches(@"import \{[^}]*RouteValues[^}]*\} from ""@equantic/runtime""", result.TypeScript);
    }
    /// <summary>
    /// A private helper called <c>Render</c> is a HELPER, not the component's entry point.
    /// <para>
    /// The parser matched the entry point by NAME, so any method called <c>Render</c> became the
    /// build method — and the last one in the file won. A page with a
    /// <c>private static VisualNode Render(DocBlock block, IAppTheme theme)</c> had its Build body
    /// REPLACED by the helper's and the helper dropped entirely, so the emitted
    /// <c>build(_context)</c> referenced parameters that do not exist in that scope.
    /// </para>
    /// <para>
    /// The page then died at hydration on "block is not defined" while SSR rendered correct HTML
    /// and answered 200 — a curl saw a perfect page and only a browser saw the failure, which is
    /// exactly the shape a smoke test cannot catch.
    /// </para>
    /// </summary>
    [Fact]
    public void WithRefs_APrivateHelperNamedRender_IsAHelper_NotTheBuildMethod()
    {
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Doc : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Render("heading", context.Theme);

                private static VisualNode Render(string kind, IAppTheme theme)
                {
                    switch (kind)
                    {
                        case "heading": return Text("H", TypeRole.Heading);
                        default: return Text("P", TypeRole.BodyM);
                    }
                }
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));

        // build keeps its OWN body — the call, not the helper's switch.
        Assert.Contains("build(context", result.TypeScript);
        Assert.Contains("return Doc.render(", result.TypeScript);

        // And the helper survives, with its own parameters in its own scope.
        Assert.Contains("static render(kind", result.TypeScript);
        Assert.DoesNotContain("build(_context: BuildContext) {\n        switch", result.TypeScript);
    }

    /// <summary>The same shape one step further: a helper called <c>Build</c>. Both names were
    /// matched, so both could steal the entry point.</summary>
    [Fact]
    public void WithRefs_APrivateHelperNamedBuild_IsAHelperToo()
    {
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Panel : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Build("x");

                private static VisualNode Build(string label) => Text(label, TypeRole.BodyM);
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("static build(label", result.TypeScript);
    }

}
