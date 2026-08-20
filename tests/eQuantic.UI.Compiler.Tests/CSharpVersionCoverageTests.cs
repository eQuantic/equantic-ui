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
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
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
        Assert.Contains("set message(value) { this.$message = value ?? ''; }", probe.TypeScript);
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
        // C# 13 `\e` folds to the real ESC character in the emitted literal.
        Assert.Contains("[0m", probe.TypeScript);
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
        // `_s[0]` made the first arm always win and read undefined.
        Assert.Contains("instanceof ClosedGate", probe.TypeScript);
        Assert.Contains("instanceof OpenGate", probe.TypeScript);
        Assert.Contains("_s.percent", probe.TypeScript);
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
