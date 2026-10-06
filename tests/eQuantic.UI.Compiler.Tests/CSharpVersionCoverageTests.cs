using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Language-version coverage, pinned per feature: C# 13 and 14 as shipped, C# 15 as previewed by
/// the Roslyn this compiler embeds (eqc parses with <see cref="ParseDefaults"/> — Preview — so it
/// never chokes before csc has its say). Each case states what the EMISSION must be, or which EQ
/// diagnostic fences the form until it has a lowering — never a silent wrong answer.
/// </summary>
public class CSharpVersionCoverageTests
{
    private static List<CompilationResult> Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs").ToList();
    }

    private static CompilationResult One(string source, string name) =>
        Compile(source).Single(r => r.ComponentName == name);

    private const string Head = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using eQuantic.UI.Primitives;

        """;

    // ---- C# 14 -----------------------------------------------------------------------------------

    [Fact]
    public void NullConditionalAssignment_GuardsOnce_AndAssignsBehindTheGuard()
    {
        var probe = One(Head + """
            public sealed class Model { public string Label { get; set; } = ""; }
            public sealed class Probe : StatelessComponent
            {
                private Model? _model = new();
                public override VisualNode Build(ComponentContext context)
                {
                    _model?.Label = "hi";
                    _model?.Label += "!";
                    return new Box();
                }
            }
            """, "Probe");

        Assert.True(probe.Success, string.Join("\n", probe.Errors.Select(e => e.Message)));
        Assert.Contains("$t == null ? null : ($t.label = 'hi')", probe.TypeScript);
        Assert.Contains("$t == null ? null : ($t.label += '!')", probe.TypeScript);
        // JS rejects `?.` on an assignment target — the raw shape must never survive.
        Assert.DoesNotContain("?.label =", probe.TypeScript);
    }

    [Fact]
    public void FieldKeyword_EmitsABackingSlotAndGuardedAccessors()
    {
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public string Message { get; set => field = value ?? ""; }
                public override VisualNode Build(ComponentContext context) => new Text(Message, TypeRole.BodyM, null);
            }
            """, "Probe");

        Assert.True(probe.Success);
        Assert.Contains("$message", probe.TypeScript);
        Assert.Contains("set message(value) {\n        this.$message = value ?? '';\n    }", probe.TypeScript);
    }

    [Fact]
    public void AStaticFieldKeyword_KeepsItsSlotOnTheClass()
    {
        // A static accessor's `this` is the class, so the store it names has to be the class's: it was
        // declared on the instance, which the module's type check refuses (#483).
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public static int Total { get; set => field = value * 2; }
                public override VisualNode Build(ComponentContext context) => new Text(Total.ToString(), TypeRole.BodyM, null);
            }
            """, "Probe");

        Assert.True(probe.Success, string.Join("\n", probe.Errors.Select(e => e.Message)));
        Assert.Contains("static $total: number = 0;", probe.TypeScript);
        Assert.DoesNotContain("declare static $total", probe.TypeScript);
        Assert.Contains("static set total(value) {\n        this.$total = value * 2;\n    }", probe.TypeScript);
    }

    [Fact]
    public void AStaticStore_StartsAsItsInitializerOrItsTypesDefault()
    {
        // No constructor runs for a static, so its store starts on the declaration, as C# starts it:
        // the initializer, written into the store directly, or the type's default. A component's was
        // declared alone and read undefined until the first write, a plain class dropped the
        // initializer, and a static auto-property with none read undefined where C# reads 0 (#483).
        var component = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public static int Limit { get; set => field = value * 2; } = 5;
                public static string Label { get; set => field = value.Trim(); }
                public static int Hits { get; set; }
                public override VisualNode Build(ComponentContext context) => new Text(Label + Limit + Hits, TypeRole.BodyM, null);
            }
            """, "Probe");
        var plain = One(Head + """
            public static class Counter
            {
                public static int Total { get; set => field = value * 2; } = 5;
            }
            """, "Counter");

        Assert.True(component.Success, string.Join("\n", component.Errors.Select(e => e.Message)));
        Assert.Contains("static $limit: number = 5;", component.TypeScript);
        Assert.Contains("declare static $label: string;", component.TypeScript);
        Assert.Contains("static hits: number = 0;", component.TypeScript);
        Assert.True(plain.Success, string.Join("\n", plain.Errors.Select(e => e.Message)));
        Assert.Contains("static $total: number = 5;", plain.TypeScript);
    }

    [Fact]
    public void ExtensionBlockMembers_LowerToStatics_AndCallSitesFollow()
    {
        var results = Compile(Head + """
            public static class SeqExtensions
            {
                extension(IEnumerable<int> source)
                {
                    public bool IsEmpty => !source.Any();
                    public int DoubledFirst() => source.First() * 2;
                }
            }

            public sealed class Probe : StatelessComponent
            {
                private readonly List<int> _values = [1, 2, 3];
                public override VisualNode Build(ComponentContext context)
                {
                    var empty = _values.IsEmpty;
                    var doubled = _values.DoubledFirst();
                    return new Text($"{empty} {doubled}", TypeRole.BodyM, null);
                }
            }
            """);

        var extensions = results.Single(r => r.ComponentName == "SeqExtensions");
        Assert.Contains("static isEmpty(source", extensions.TypeScript);
        Assert.Contains("static doubledFirst(source", extensions.TypeScript);

        var probe = results.Single(r => r.ComponentName == "Probe");
        Assert.Contains("SeqExtensions.isEmpty(this._values)", probe.TypeScript);
        Assert.Contains("SeqExtensions.doubledFirst(this._values)", probe.TypeScript);
        Assert.Contains("import { SeqExtensions }", probe.TypeScript);
    }

    [Fact]
    public void AnExtensionMember_LowersAsAnyMethod_AnIteratorAndATaskIncluded()
    {
        // One method lowering for an extension member too (#432): an iterator wrote `yield` outside a
        // generator, and a method returning a type named TaskItem was made async by the name alone.
        var results = Compile(Head + """
            using System.Threading.Tasks;

            public sealed record TaskItem(int N);

            public static class CountExtensions
            {
                extension(int count)
                {
                    public IEnumerable<int> Upto() { for (var i = 0; i < count; i++) yield return i; }
                    public TaskItem Item() => new TaskItem(count);
                    public Task<int> Later() => Task.FromResult(count);
                }
            }

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text($"{string.Join(",", 3.Upto())} {3.Item().N}", TypeRole.BodyM, null);
            }
            """);

        var extensions = results.Single(r => r.ComponentName == "CountExtensions").TypeScript;
        Assert.Contains("const _seq = [];", extensions);
        Assert.DoesNotContain("yield", extensions);
        Assert.Contains("static item(count", extensions);
        Assert.DoesNotContain("async item(", extensions);
        Assert.Contains("static async later(count", extensions);
    }

    [Fact]
    public void AClassAndAComponent_AskTheReturnTypeWhetherAMethodIsAsync_AndAGetterMayYield()
    {
        // Found in review (#432): a method returning a type called TaskItem was made async by its
        // name, in a class and in a component, and a getter that yields wrote `yield` outside a
        // generator in both.
        var results = Compile(Head + """
            using System.Threading.Tasks;

            public sealed record TaskItem(int N);

            public class Bucket
            {
                public TaskItem Make(int n) => new TaskItem(n);
                public Task<int> Later() => Task.FromResult(1);
                public IEnumerable<int> Items { get { yield return 1; yield return 2; } }
            }

            public sealed class Probe : StatelessComponent
            {
                private TaskItem Make(int n) => new TaskItem(n);
                private IEnumerable<int> Numbers { get { yield return 3; } }
                public override VisualNode Build(ComponentContext context) =>
                    new Text($"{Make(1).N} {new Bucket().Make(2).N} {string.Join(",", Numbers)}", TypeRole.BodyM, null);
            }
            """);

        var bucket = results.Single(r => r.ComponentName == "Bucket").TypeScript;
        Assert.Contains("make(n", bucket);
        Assert.DoesNotContain("async make(", bucket);
        Assert.Contains("async later(", bucket);
        Assert.Contains("const _seq = [];", bucket);
        Assert.DoesNotContain("yield", bucket);

        var probe = results.Single(r => r.ComponentName == "Probe").TypeScript;
        Assert.Contains("make(n", probe);
        Assert.DoesNotContain("async make(", probe);
        Assert.Contains("const _seq = [];", probe);
        Assert.DoesNotContain("yield", probe);
    }

    [Fact]
    public void AnAccessorBlock_DeclaresTheLocalItsOutVarBinds()
    {
        // An accessor's block is lowered as a method's (#432): the local an `out var` binds is
        // declared in front. A class's getter converted the block alone until #484 declared each
        // statement's own variables, and the shared lowering keeps it.
        var results = Compile(Head + """
            public class Bucket
            {
                public string Text { get; set; } = "7";
                public int Parsed { get { return int.TryParse(Text, out var parsed) ? parsed : -1; } }
            }
            """);

        var bucket = results.Single(r => r.ComponentName == "Bucket").TypeScript;
        Assert.Matches(@"get parsed\(\)[^{]*\{\s*let parsed", bucket);
    }

    [Fact]
    public void OutLambda_HonoursTheCalleeContract()
    {
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                private delegate bool TryGet(string text, out int result);
                public override VisualNode Build(ComponentContext context)
                {
                    TryGet parse = (text, out result) => int.TryParse(text, out result);
                    var ok = parse("42", out var n);
                    return new Text($"{ok} {n}", TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        Assert.True(probe.Success);
        // Callee returns the {outs, $} object the call-site unwrap reads.
        Assert.Contains("return { $: $r, result }", probe.TypeScript);
        Assert.Contains("$o.result", probe.TypeScript);
    }

    [Fact]
    public void NameofUnboundGeneric_FoldsToTheConstant()
    {
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                    => new Text(nameof(List<>), TypeRole.BodyM, null);
            }
            """, "Probe");

        Assert.Contains("'List'", probe.TypeScript);
    }

    [Fact]
    public void PartialDeclarationsInOneFile_AreABuildError_NotASilentDoubleModule()
    {
        var results = Compile(Head + """
            public sealed partial class Probe : StatelessComponent
            {
                public partial string Title { get; }
            }

            public sealed partial class Probe
            {
                public partial string Title => "t";
                public override VisualNode Build(ComponentContext context) => new Text(Title, TypeRole.BodyM, null);
            }
            """);

        Assert.All(results.Where(r => r.ComponentName == "Probe"),
            r => Assert.Contains(r.Errors, e => e.Code == "EQ2009"));
    }

    // ---- C# 13 -----------------------------------------------------------------------------------

    [Fact]
    public void ThreadingLock_LowersToAnInertObject()
    {
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                private readonly System.Threading.Lock _gate = new();
                public override VisualNode Build(ComponentContext context)
                {
                    var marker = "\e[0m";
                    lock (_gate) { marker += "x"; }
                    return new Text(marker, TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        Assert.True(probe.Success);
        Assert.Contains("= {};", probe.TypeScript);
        Assert.DoesNotContain("new Lock()", probe.TypeScript);
        // C# 13 `\e` is the ESC character, written as an escape: a control is invisible in the
        // module, so none is written raw (#520). The char overload compares ordinally; a string's
        // is culture-aware, and ICU ignores a control, so it finds one at position 0 of anything.
        Assert.Contains(@"'\u001B[0m'", probe.TypeScript);
        Assert.DoesNotContain('\u001B', probe.TypeScript);
    }

    [Fact]
    public void ImplicitIndexInitializer_IsFenced_NotInvalidJs()
    {
        var probe = One(Head + """
            public sealed class Holder { public int[] Buffer { get; set; } = new int[4]; }
            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var h = new Holder { Buffer = { [^1] = 9 } };
                    return new Text($"{h.Buffer[3]}", TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        Assert.False(probe.Success);
        Assert.Contains(probe.Errors, e => e.Code == "EQ2008");
        Assert.DoesNotContain("[^1]", probe.TypeScript);
    }

    // ---- C# 15 (preview — parsed by the embedded Roslyn under LanguageVersion.Preview) ----------

    [Fact]
    public void LabeledBreakAndContinue_RideThroughAsJsLabels()
    {
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var total = 0;
                    outer: for (var i = 0; i < 3; i++)
                    {
                        for (var j = 0; j < 3; j++)
                        {
                            if (j == 2) continue outer;
                            if (i == 2) break outer;
                            total += i * 10 + j;
                        }
                    }
                    return new Text($"{total}", TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        Assert.True(probe.Success);
        Assert.Contains("outer: for", probe.TypeScript);
        Assert.Contains("continue outer;", probe.TypeScript);
        Assert.Contains("break outer;", probe.TypeScript);
    }

    [Fact]
    public void ALabeledLoopWhoseConditionBindsATemporary_DeclaresItInFrontOfTheLabel()
    {
        // A null-conditional tail behind a call binds a temporary its statement declares (#539). The
        // declaration goes in front of the label: between the label and its loop it would brace the
        // loop away from the label, and `continue outer` is then a SyntaxError that costs the module.
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                private int _at;
                private string Next() => _at < 3 ? " x " : null;
                public override VisualNode Build(ComponentContext context)
                {
                    var seen = 0;
                    outer: while (Next()?.Trim() != null)
                    {
                        _at++;
                        if (_at == 2) continue outer;
                        seen++;
                    }
                    return new Text($"{seen}", TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        Assert.True(probe.Success);
        Assert.Matches(@"let \$n0: any;\s*outer: while \(\(\(\$n0 = this\.next\(\)\) == null \? null : \$eq\.text\.trim\(\$n0\)\)",
            probe.TypeScript);
        Assert.Contains("continue outer;", probe.TypeScript);
    }

    [Fact]
    public void ALabeledLoopWhoseVariableIsCaptured_KeepsItsLabelOnTheLoop()
    {
        // The captured variable moves in front of the loop, in a block of its own (#476): the label
        // has to stay on the loop, or `continue outer` is a SyntaxError that costs the module.
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var fs = new System.Collections.Generic.List<System.Func<int>>();
                    outer: for (var i = 0; i < 3; i++)
                    {
                        fs.Add(() => i);
                        if (i == 1) continue outer;
                    }
                    return new Text($"{fs[0]()}", TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        Assert.True(probe.Success, string.Join("\n", probe.Errors.Select(e => e.Message)));
        Assert.Contains("let i = 0;", probe.TypeScript);
        Assert.Contains("outer: for (; i < 3; i++)", probe.TypeScript);
        Assert.DoesNotContain("outer: {", probe.TypeScript);
    }

    [Fact]
    public void CollectionWithArguments_CapacityDrops_ComparerIsFenced()
    {
        var probe = One(Head + """
            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    string[] values = ["one", "two"];
                    List<string> names = [with(capacity: 8), .. values];
                    HashSet<string> set = [with(StringComparer.OrdinalIgnoreCase), "Hello", "HELLO"];
                    return new Text($"{names.Count} {set.Count}", TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        // The capacity hint vanished without a trace; the comparer is an error, because a JS Set
        // keeping two "equal" strings is a wrong answer nothing else would flag.
        Assert.Contains("[...values]", probe.TypeScript);
        Assert.DoesNotContain("with(", probe.TypeScript);
        Assert.Contains(probe.Errors, e => e.Code == "EQ2007");
    }

    [Fact]
    public void UnionDeclaration_EmitsATsUnionModule_AndInstanceofArms()
    {
        var results = Compile(Head + """
            public record class Cat(string Name);
            public record class Dog(string Name);
            public union Pet(Cat, Dog);

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    Pet pet = new Dog("Rex");
                    var name = pet switch
                    {
                        Dog d => d.Name,
                        Cat c => c.Name,
                    };
                    return new Text(name, TypeRole.BodyM, null);
                }
            }
            """);

        var union = results.Single(r => r.ComponentName == "Pet");
        Assert.True(union.Success);
        Assert.Contains("export type Pet = Cat | Dog;", union.TypeScript);
        Assert.Contains("export const Pet = undefined;", union.TypeScript);

        var probe = results.Single(r => r.ComponentName == "Probe");
        Assert.Contains("instanceof Dog", probe.TypeScript);
        Assert.Contains("instanceof Cat", probe.TypeScript);
    }

    [Fact]
    public void ClosedHierarchySwitch_TestsEachArmByType_AndDeconstructsByName()
    {
        var probe = One(Head + """
            public closed record class GateState;
            public record class ClosedGate : GateState;
            public record class OpenGate(float Percent) : GateState;

            public sealed class Probe : StatelessComponent
            {
                private readonly GateState _state = new OpenGate(50f);
                public override VisualNode Build(ComponentContext context)
                {
                    var text = _state switch
                    {
                        ClosedGate => "closed",
                        OpenGate(var percent) => $"{percent}% open",
                    };
                    return new Text(text, TypeRole.BodyM, null);
                }
            }
            """, "Probe");

        // A bare type name in an arm PARSES as a constant pattern but BINDS as a type pattern:
        // `=== ClosedGate` compared the value to the class and the arm was dead. And a positional
        // pattern must test ITS type and deconstruct by the pattern type's names — `!= null` +
        // `$s[0]` made the first arm always win and read undefined.
        Assert.Contains("instanceof ClosedGate", probe.TypeScript);
        Assert.Contains("instanceof OpenGate", probe.TypeScript);
        Assert.Contains("$s.percent", probe.TypeScript);
    }

    [Fact]
    public void ExtensionIndexer_LowersToAStaticItem_AndTheIndexFollows()
    {
        var results = Compile(Head + """
            public static class SequenceIndexer
            {
                extension(IEnumerable<int> sequence)
                {
                    public int this[int index] => sequence.ElementAt(index);
                }
            }

            public sealed class Probe : StatelessComponent
            {
                private readonly List<int> _values = [10, 20, 30];
                public override VisualNode Build(ComponentContext context)
                {
                    IEnumerable<int> seq = _values;
                    return new Text($"{seq[1]}", TypeRole.BodyM, null);
                }
            }
            """);

        Assert.Contains("static item(sequence", results.Single(r => r.ComponentName == "SequenceIndexer").TypeScript);
        Assert.Contains("SequenceIndexer.item(seq, 1)", results.Single(r => r.ComponentName == "Probe").TypeScript);
    }
}
