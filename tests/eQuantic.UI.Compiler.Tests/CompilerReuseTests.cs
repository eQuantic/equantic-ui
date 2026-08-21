using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// What happens when ONE <see cref="ComponentCompiler"/> compiles many things — the case a build
/// has always exercised (every file in a project goes through the same instance) and the case the
/// design host makes permanent (a process that stays up all day and recompiles on every keystroke).
/// </summary>
public class CompilerReuseTests
{
    /// <summary>A component that pulls in helpers: interpolation, LINQ, a lambda, a collection.</summary>
    private const string HelperHungrySource = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed class Busy : StatefulComponent
        {
            private int _count;
            private readonly List<string> _items = new();

            public override VisualNode Build(ComponentContext context)
            {
                var visible = _items.Where(i => i.Length > 2).Select(i => i.ToUpper()).ToList();
                var column = new Column(gap: Space.S3);
                column.Add(new Text($"Count: {_count} of {visible.Count}", TypeRole.Display, context.Theme.TextPrimary));
                column.Add(new Button("Up", onPressed: () => SetState(() => _count++)));
                return column;
            }
        }
        """;

    /// <summary>A component that needs almost nothing — its import line is the thing under test.</summary>
    private const string FrugalSource = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed class Frugal : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                return new Text("hello", TypeRole.BodyM, context.Theme.TextPrimary);
            }
        }
        """;

    private static string ImportsOf(string typeScript) => string.Join
    (
        "\n",
        typeScript.Split('\n').Where(l => l.StartsWith("import", StringComparison.Ordinal))
    );

    /// <summary>
    /// A component's output must not depend on what the compiler happened to compile BEFORE it.
    /// Today two independent things uphold this — <c>GenerateImports</c> intersects the collected
    /// helper names with the identifiers the emitted body actually mentions, and
    /// <c>ConversionContext.Reset</c> drops the names anyway — and the invariant is worth pinning
    /// because losing EITHER of them turns compile order into output.
    /// </summary>
    [Fact]
    public void AComponentsImports_DoNotDependOnWhatWasCompiledBeforeIt()
    {
        var fresh = new ComponentCompiler { TypeAnnotations = false };
        var alone = ImportsOf(fresh.CompileSource(FrugalSource, "Frugal.cs").Single().TypeScript);

        var shared = new ComponentCompiler { TypeAnnotations = false };
        shared.CompileSource(HelperHungrySource, "Busy.cs").Single();
        var afterBusy = ImportsOf(shared.CompileSource(FrugalSource, "Frugal.cs").Single().TypeScript);

        Assert.Equal(alone, afterBusy);
    }

    /// <summary>
    /// <c>Reset</c> drops the node cache, which is the entry a design host cares about: it is keyed
    /// by <c>SyntaxNode</c>, so each entry holds that node's whole tree, and nothing used to drop it
    /// between files. Measured in isolation: <b>38.4 KB</b> retained per compile before, <b>4.7 KB</b>
    /// after.
    /// <para>
    /// Asserted as the CONTRACT rather than as its memory consequence, after two attempts at the
    /// latter proved unattributable. A <c>GC.GetTotalMemory</c> reading is the whole process's, so it
    /// passed alone and failed in a full run — it was measuring six hundred other tests. And
    /// reachability is no better: a control that never compiled at all still found the tree alive,
    /// because Roslyn keeps its own caches of recently parsed trees. What this compiler promises is
    /// that it holds nothing after a reset, and that is what is checked here.
    /// </para>
    /// </summary>
    [Fact]
    public void Reset_DropsTheNodeCache_SoNoTreeSurvivesTheFileItBelongedTo()
    {
        var context = new eQuantic.UI.Compiler.CodeGen.ConversionContext
        {
            Converter = new eQuantic.UI.Compiler.CodeGen.CSharpToJsConverter(),
            SemanticHelper = new eQuantic.UI.Compiler.Services.SemanticHelper(null),
        };

        var node = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree
            .ParseText("class Probe { void M() { var x = 1; } }").GetRoot();

        var cached = eQuantic.UI.Compiler.CodeGen.Ir.JsExpr.Opaque("cached");
        context.SetCached(node, cached);
        Assert.Same(cached, context.GetCached(node));

        context.Reset();

        Assert.Null(context.GetCached(node));
    }
}
