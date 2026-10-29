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
        using eQuantic.UI.Primitives;

        public sealed class Frugal : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                return new Text("hello", TypeRole.BodyM, context.Theme.TextPrimary);
            }
        }
        """;

    /// <summary>A plain class that binds two temporaries, a call receiver's null-conditional tail
    /// in each of two statements (#539).</summary>
    private const string TwoTemporariesSource = """
        public class First
        {
            private string Get() => " a ";

            public string Both()
            {
                var a = Get()?.Trim();
                var b = Get()?.Trim();
                return a + b;
            }
        }
        """;

    /// <summary>A plain class that binds one.</summary>
    private const string PlainClassSource = """
        public class Second
        {
            private string Name() => " b ";

            public string Upper()
            {
                return Name()?.ToUpperInvariant();
            }
        }
        """;

    /// <summary>A static helper that binds one.</summary>
    private const string StaticHelperSource = """
        public static class Third
        {
            private static string Name() => " c ";

            public static string Upper()
            {
                return Name()?.ToUpperInvariant();
            }
        }
        """;

    /// <summary>Two records that bind a temporary each, in one file.</summary>
    private const string RecordsSource = """
        public record Former(string Text)
        {
            private string Get() => Text;

            public string Both()
            {
                var a = Get()?.Trim();
                var b = Get()?.Trim();
                return a + b;
            }
        }

        public record Latter(string Text)
        {
            private string Get() => Text;

            public string Upper()
            {
                return Get()?.ToUpperInvariant();
            }
        }
        """;

    /// <summary>The first temporary a module names.</summary>
    private static string FirstTemporaryOf(string module) =>
        System.Text.RegularExpressions.Regex.Match(module, @"\$n\d+").Value;

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
    /// A plain class's and a static helper's module start from a reset converter, as a component's
    /// does (#1138). Their emitter cleared the names it used and the diagnostics, and nothing else, so
    /// a module numbered its temporaries after the module written before it (the runtime's
    /// <c>SheetController</c> twin began at <c>$n49</c>), and an edit to one class renumbered every
    /// twin written after it. Asked of the output, byte for byte: whatever was compiled before, the
    /// module is the one it is alone.
    /// </summary>
    [Theory]
    [InlineData(PlainClassSource, "Second.cs")]
    [InlineData(StaticHelperSource, "Third.cs")]
    public void AClassModule_NumbersItsOwnTemporaries_WhateverWasCompiledBeforeIt(string source, string path)
    {
        var alone = new ComponentCompiler { TypeAnnotations = false }.CompileSource(source, path).Single().TypeScript;

        var shared = new ComponentCompiler { TypeAnnotations = false };
        var before = shared.CompileSource(TwoTemporariesSource, "First.cs").Single().TypeScript;
        Assert.Contains("let $n1;", before);
        var after = shared.CompileSource(source, path).Single().TypeScript;

        Assert.Equal("$n0", FirstTemporaryOf(after));
        Assert.Equal(alone, after);
    }

    /// <summary>
    /// The record emitter's module starts from a reset converter too, which no build of the compiler
    /// shows, since it hands each record a converter of its own: a host that shares one, as the
    /// conformance harness shares one between a prelude's records, met the same numbering.
    /// </summary>
    [Fact]
    public void ARecordModule_NumbersItsOwnTemporaries_ThroughAConverterItShares()
    {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(RecordsSource,
            eQuantic.UI.Compiler.Services.ParseDefaults.Options);
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Records", [tree],
            [TestReferences.Of(typeof(object).Assembly.Location)],
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var records = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax>().ToList();

        string Module(eQuantic.UI.Compiler.CodeGen.CSharpToJsConverter converter, Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax record) =>
            new eQuantic.UI.Compiler.CodeGen.RecordTypeEmitter(converter).EmitModule(record, tsTypeDeclarations: false);
        eQuantic.UI.Compiler.CodeGen.CSharpToJsConverter Converter()
        {
            var converter = new eQuantic.UI.Compiler.CodeGen.CSharpToJsConverter();
            converter.SetSemanticModel(model);
            return converter;
        }

        var alone = Module(Converter(), records[1]);
        var shared = Converter();
        Assert.Contains("let $n1;", Module(shared, records[0]));
        var after = Module(shared, records[1]);

        Assert.Equal("$n0", FirstTemporaryOf(after));
        Assert.Equal(alone, after);
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
