using eQuantic.UI.Compiler;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A plain class's property that guards its own store with <c>field</c>, run in the browser and
/// read back as .NET reads it. The statement harness emits a prelude's records alone, so the classes
/// go through the compiler the SDK runs, and the module it writes is executed with a driver (#483).
/// </summary>
public class PlainClassStoreConformanceTests
{
    private const string Source = """
        public static class Counter
        {
            public static int Total { get; set => field = value * 2; } = 5;
        }

        public class Tally
        {
            public static int Count { get; set => field = value + 1; }
        }
        """;

    /// <summary>
    /// The store starts as the initializer, or the type's default, and a write goes through the
    /// setter: the class wrote a field named like the property beside the setter, which shadowed it,
    /// so `Total = 3` read back 3 where C# reads 6. .NET answers 5, 6, 0 and 2.
    /// </summary>
    [SkippableFact]
    public void AStaticFieldBackedProperty_StartsAsItsInitializer_AndWritesThroughItsSetter()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var modules = new ComponentCompiler { TypeAnnotations = false }.CompileSource(Source, "Stores.cs")
            .Where(result => result.TypeScript.Length > 0)
            .ToList();
        Assert.All(modules, module => Assert.True(module.Success,
            $"{module.ComponentName}: {string.Join("\n", module.Errors.Select(e => e.Message))}"));

        var program = string.Join("\n", modules.Select(module => module.TypeScript)) + """

            const total = Counter.total;
            Counter.total = 3;
            const count = Tally.count;
            Tally.count = 1;
            console.log(JSON.stringify([total, Counter.total, count, Tally.count]));
            """;

        Assert.Equal("[5,6,0,2]", JsExecutor.Run(program));
    }
}
