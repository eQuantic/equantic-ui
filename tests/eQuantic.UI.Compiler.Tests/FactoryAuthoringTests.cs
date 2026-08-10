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


    /// <summary>
    /// A pattern variable is DECLARED wherever it is bound, not only in the three statement
    /// positions that happened to ask for it.
    /// <para>
    /// `x is { } y` converts to an assignment of `y` inside the converted condition, so the `let`
    /// has to be hoisted. Only Return, ExpressionStatement, LocalDeclaration and If did that; a
    /// switch-expression arm, an expression-bodied member, a lambda body and a while condition
    /// assigned a name that was never declared — a ReferenceError in a module, which is strict
    /// mode, the first time that code ran.
    /// </para>
    /// <para>
    /// SSR never saw it: the server runs the C#. The browser threw on hydration and the boundary
    /// swapped the subtree for its failure message, so the page looked right until it did not.
    /// </para>
    /// </summary>
    [Theory]
    // The minimal repro, and the shape that found it: a switch expression inside an expression body.
    [InlineData("private static string Broken(string v) => Maybe(v) is { } bound ? bound : \"\";")]
    [InlineData("""
        private static string Arm(string k, string v) => k switch
        {
            "a" => Maybe(v) is { } bound ? bound : "",
            _ => "",
        };
        """)]
    [InlineData("""
        private static Func<string, string> Lambda() => (v) => Maybe(v) is { } bound ? bound : "";
        """)]
    [InlineData("""
        private static string Loop(string v)
        {
            var n = 0;
            var last = "";
            while (Maybe(v) is { } bound && n < 1) { last = bound; n++; }
            return last;
        }
        """)]
    public void WithRefs_EveryPatternVariable_IsDeclaredWhereItIsBound(string member)
    {
        var result = CompileWithRefs($$"""
            using System;
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Text("x", TypeRole.BodyM);

                private static string Maybe(string v) => v;

                {{member}}
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));

        // It is BOUND (the converted condition assigns it) …
        Assert.Contains("bound = ", result.TypeScript);
        // … so it must be DECLARED. Exactly once: a second `let` in an enclosing scope means the
        // scanner leaked the binding out of the scope that owns it.
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(result.TypeScript, @"let bound: any;").Count);
    }


    /// <summary>
    /// `x is { } y` over a CALL evaluates the call ONCE.
    /// <para>
    /// The subject appears in the condition and again in the binding, so it ran twice — wasted
    /// work when the call is pure, and a different answer when it is not. The optimisation for it
    /// was written but gated on a string comparison against `x != null`, while the condition
    /// builder produces `(x != null)`: it had never once run.
    /// </para>
    /// </summary>
    [Fact]
    public void WithRefs_APresencePatternOverACall_EvaluatesItOnce()
    {
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Once : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Text(Pick("v"), TypeRole.BodyM);

                private static string Maybe(string v) => v;

                private static string Pick(string v) => Maybe(v) is { } bound ? bound : "";
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("(bound = Once.maybe(v)) != null", result.TypeScript);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(result.TypeScript, @"Once\.maybe\(v\)").Count);
    }

    /// <summary>A TYPE test must still check before it assigns — the optimisation applies to
    /// presence only, and `x is string s` assigning on a non-string would bind the wrong value.</summary>
    [Fact]
    public void WithRefs_ATypePattern_StillTestsBeforeItBinds()
    {
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Typed : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Text(Pick("v"), TypeRole.BodyM);

                private static object Maybe(string v) => v;

                private static string Pick(string v) => Maybe(v) is string bound ? bound : "";
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("typeof", result.TypeScript);
        Assert.DoesNotContain("(bound = Typed.maybe(v)) != null", result.TypeScript);
    }
}
