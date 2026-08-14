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
    /// A design host compiles on every pause in typing, for hours, through one compiler. The node
    /// cache used to hold every tree it had ever seen. Measured on this suite: <b>38.4 KB</b>
    /// retained per compile before <c>ConversionContext.Reset</c> cleared it, <b>4.7 KB</b> after —
    /// so the bound below sits between the two and fails if the reset is lost. It asserts "does not
    /// grow with the number of compiles", not an allocation budget; the residual 4.7 KB is Roslyn's
    /// own per-compilation state, which is a separate question from this one.
    /// </summary>
    [Fact]
    public void CompilingRepeatedly_DoesNotGrowTheHeapWithEachCompile()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false };

        // Warm up: first compiles pay for JIT and Roslyn's own one-time tables, which are not a leak.
        for (var i = 0; i < 20; i++) compiler.CompileSource(HelperHungrySource, "Busy.cs").Single();

        var before = GC.GetTotalMemory(forceFullCollection: true);

        const int compiles = 200;
        for (var i = 0; i < compiles; i++)
        {
            // A distinct source each time: identical text would let Roslyn's own caching hide a leak.
            var source = HelperHungrySource.Replace("Busy", $"Busy{i}");
            compiler.CompileSource(source, $"Busy{i}.cs").Single();
        }

        var after = GC.GetTotalMemory(forceFullCollection: true);
        var growthPerCompile = (after - before) / (double)compiles;

        Assert.True(
            growthPerCompile < 16 * 1024,
            $"retained {growthPerCompile / 1024:F1} KB per compile over {compiles} compiles "
            + $"({(after - before) / 1024.0 / 1024.0:F1} MB total) — the converter is holding onto "
            + "per-file state again (ConversionContext.Reset).");
    }
}
