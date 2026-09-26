using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// What the emitted TypeScript SAYS a value is, against what the runtime actually hands over.
/// <para>
/// An annotation is not decoration here: the transpiled modules are typechecked by the runtime's
/// own build, and an app's editor reads the generated factory surface. A wrong one is a lie the
/// rest of the file then typechecks against — the `number` these cases pin used to sit over a
/// `bigint` and over a class with methods on it, so `unit.mul(...)` was reachable on a "number".
/// </para>
/// </summary>
public class EmittedTypeSurfaceTests
{
    [Fact]
    public void ALongIsABigint_NotANumber()
    {
        var ts = TestHelper.ConvertClass("""
            public long Ticks { get; init; }
            public long Doubled(long n) => n * 2;
            """);

        // The literal the emitter writes for the field IS a bigint, and `n * 2` emits `n * 2n`.
        ts.Should().Contain("ticks: bigint = $eq.num.long(0)");
        ts.Should().Contain("doubled(n: bigint)");
        ts.Should().NotContain(": number = $eq.num.long");
    }

    [Fact]
    public void ADecimalIsTheRuntimeClass_AndArrivesImported()
    {
        var ts = TestHelper.ConvertClass("""
            public decimal Price { get; init; }
            public decimal Total(decimal unit, int count) => unit * count;
            """);

        ts.Should().Contain("price: Decimal = $eq.num.dec(0)");
        ts.Should().Contain("total(unit: Decimal, count: number)");
        // The name the TRANSLATION invents — no syntax walk can see it, so the import is the half
        // of this fix that a mapping change alone would have missed.
        ts.Should().Contain("import { $eq, Decimal } from \"@equantic/runtime\"");
    }

    [Fact]
    public void ADateTimeIsTheRuntimeTicks_NotTheBrowsersDate()
    {
        var ts = TestHelper.ConvertClass("""
            public DateTime When { get; init; }
            public DateTime Later(DateTime from) => from.AddDays(1);
            """);

        ts.Should().Contain("later(from: DateTime)");
        // `Date` on its own, which is a DIFFERENT class — the negative has to stop short of the
        // name it is a prefix of, or it passes on the very output it is meant to reject.
        Regex.IsMatch(ts, @":\s*Date(?![A-Za-z])").Should().BeFalse("the JS Date is not this type");
    }

    /// <summary>
    /// An element that is a union or a function is parenthesized before its array: TypeScript binds
    /// `[]` tighter than `|` and `=>`, so `string | null[]` is a string OR an array of nulls, and
    /// `() => void[]` a function returning an array. The code engine's row map keeps a
    /// <c>List&lt;string?&gt;</c>, and its twin failed the runtime's own build at the first `push`.
    /// </summary>
    [Fact]
    public void AUnionOrAFunctionElement_IsParenthesizedBeforeItsArray()
    {
        var ts = TestHelper.ConvertClass("""
            private readonly List<string?> _labels = new();
            public string?[] Names { get; init; } = [];
            public IReadOnlyList<int?> Counts { get; init; } = [];
            public List<Action> Callbacks { get; init; } = new();
            public List<Action?> Handlers { get; init; } = new();
            public string?[][] Grid { get; init; } = [];
            public List<string> Plain { get; init; } = new();
            """);

        ts.Should().Contain("_labels: (string | null)[]");
        ts.Should().Contain("names: (string | null)[]");
        ts.Should().Contain("counts: (number | null)[]");
        ts.Should().Contain("callbacks: (() => void)[]");
        ts.Should().Contain("handlers: ((() => void) | null)[]");
        ts.Should().Contain("grid: (string | null)[][]");
        ts.Should().Contain("plain: string[]", "an element that is a plain name needs nothing");
        ts.Should().NotContain("| null[]");
    }

    /// <summary>
    /// An ordering's key selector is typed by the element it compares. It was written to a bare
    /// <c>const _k = (filler) => …</c> inside the comparator, where nothing gives the lambda a type,
    /// and the runtime's own build refused the implicit <c>any</c> (the code engine's row map sorts
    /// its fillers). Typed through the comparator's own parameter, the key is checked again. Plain
    /// JavaScript, which the conformance harness and the playground run, carries no annotation.
    /// </summary>
    [Fact]
    public void AnOrderingsKeySelector_IsTypedByTheElementItCompares()
    {
        var ts = TestHelper.ConvertClass("""
            public List<string> Sorted(List<string> items) => items.OrderBy(item => item.Length).ThenBy(item => item).ToList();
            """);

        // The lambda's own annotation, when it has one, is the same type: the key is typed either way.
        System.Text.RegularExpressions.Regex.Matches(ts, @"const _k: \(x: typeof a\) => any = \(item(: string)?\) => item")
            .Count.Should().Be(2, "both keys of the ordering are typed through the element they compare");
        TestHelper.ConvertExpression("new List<string>().OrderBy(item => item.Length)")
            .Should().Contain("const _k = (item) => item.length;").And.NotContain("typeof a");
    }

    /// <summary>
    /// A tuple crosses as an array literal, which TypeScript reads as an array of the union of its
    /// elements: destructured into two lists of different things, each came out a list of either,
    /// and the runtime's own build refused every use of them (the diff layout's runs and folds). A
    /// method returning one says which, on a class and on a record alike, and a nullable one that it
    /// may be null. Any other return is left to inference, which reads it right.
    /// </summary>
    [Fact]
    public void AMethodReturningATuple_SaysWhichTuple()
    {
        var ts = TestHelper.ConvertClass("""
            public (List<int> Runs, List<string> Names) Parts(int n) => (new List<int> { n }, new List<string> { "x" });
            public (int Line, int Count)? Range(string text) => text.Length > 0 ? (1, 2) : null;
            public int Plain() => 1;
            """);

        ts.Should().Contain("parts(n: number): [number[], string[]]");
        ts.Should().Contain("range(text: string): [number, number] | null");
        ts.Should().Contain("plain() {");

        var record = new ComponentCompiler().CompileSource("""
            using System.Collections.Generic;

            public sealed record Layout(int Rows)
            {
                public static (List<int> Runs, List<string> Names) Parts(int n) => (new List<int> { n }, new List<string>());
            }
            """).Single(result => result.ComponentName == "Layout").TypeScript;
        record.Should().Contain("static parts(n: number): [number[], string[]]");
    }

    /// <summary>
    /// A record says the same about a value as a class does. Its mapper wrote its own nullable union,
    /// so a <c>Func&lt;int, string&gt;?</c> parameter came out <c>(value: number) =&gt; string | null</c>,
    /// a function that returns null where C# declares a function that may be missing (the diff
    /// layout's fold label), and a member or a tuple return typed decimal named <c>Decimal</c> in a
    /// module that never imported it, which the runtime's own build refuses.
    /// </summary>
    [Fact]
    public void ARecordsNullableDelegate_IsANullableFunction_AndItsDecimalArrivesImported()
    {
        var record = new ComponentCompiler().CompileSource("""
            using System;

            public sealed record Invoice(decimal Amount)
            {
                public (decimal Net, decimal Tax) Split() => (Amount * 0.8m, Amount * 0.2m);
                public static string Label(int count, Func<int, string>? label = null) => label?.Invoke(count) ?? "";
            }
            """).Single(result => result.ComponentName == "Invoice").TypeScript;

        record.Should().Contain("label: ((value: number) => string) | null");
        record.Should().Contain("split(): [Decimal, Decimal]");
        record.Should().Contain("import { $eq, Decimal } from \"@equantic/runtime\"");
    }

    /// <summary>
    /// A component's function-typed parameter keeps its type, so a lambda passed to it is typed by
    /// it. The resolvability check read the parameter's NAME inside the function type (`value` in
    /// `(value: string) => string | null`) as a type it could not resolve, degraded the whole
    /// parameter to `any`, and the code block's gutter handed every caller's lambda an implicit any.
    /// </summary>
    [Fact]
    public void AComponentsFunctionParameter_KeepsItsType()
    {
        var ts = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            public sealed class Grid : StatelessComponent
            {
                public string Label(System.Func<string, string?> numberOf) => numberOf("a") ?? "";
                public override VisualNode Build(ComponentContext context) => new Text(Label(text => text), TypeRole.BodyM);
            }
            """, "Grid.cs").Single(result => result.ComponentName == "Grid").TypeScript;

        ts.Should().Contain("label(numberOf: (value: string) => string | null)");
    }

    /// <summary>
    /// A delegate member invoked where C# proved it not null keeps that proof in the twin. C#'s flow
    /// analysis reads a lambda with the state where the lambda is written, and TypeScript does not
    /// carry a property's narrowing into a closure, so <c>OnSelect is null ? null : () => OnSelect(i)</c>
    /// was a possibly-null call in eleven twins the moment their delegates were typed. A member C#
    /// did not prove is left as it is.
    /// </summary>
    [Fact]
    public void ADelegateMemberCSharpProvedNotNull_KeepsThatProofInTheTwin()
    {
        var source = """
            using System;
            using eQuantic.UI.Primitives;

            public sealed class Tabs : StatelessComponent
            {
                public Action<int>? OnSelect { get; init; }
                public Action? OnClose { get; init; }

                public override VisualNode Build(ComponentContext context)
                {
                    Action? press = OnSelect is null ? null : () => OnSelect(1);
                    OnClose?.Invoke();
                    return new Text("x", TypeRole.BodyM);
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "Tabs.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);

        var ts = compiler.CompileSource(source, "Tabs.cs").Single(result => result.ComponentName == "Tabs").TypeScript;

        ts.Should().Contain("() => this.onSelect!(1)", "C# proved OnSelect not null where the lambda is written");
        ts.Should().NotContain("onClose!", "a conditional call proves nothing, and needs nothing");
    }

    [Fact]
    public void TheWrappingSurvivesTheMapping()
    {
        // The name arrives inside arrays and sequences too, and the import has to follow it there.
        var ts = TestHelper.ConvertClass("""
            public decimal[] Prices { get; init; } = [];
            public long[] Stamps { get; init; } = [];
            """);

        ts.Should().Contain("prices: Decimal[]");
        ts.Should().Contain("stamps: bigint[]");
        ts.Should().Contain("Decimal } from \"@equantic/runtime\"");
    }
}
