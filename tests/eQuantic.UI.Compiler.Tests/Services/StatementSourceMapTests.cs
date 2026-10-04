using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// Every statement of a member's body maps to its own C# line (#293). The map was member-level: the
/// member's line and its body's first line, so a frame or a breakpoint anywhere in a body led to the
/// top of the method. Read back from the map the build writes, the way a debugger reads it.
/// </summary>
public class StatementSourceMapTests
{
    private const string Source = """
        namespace Demo;

        public class Tally
        {
            public int Count(int[] values)
            {
                var total = 0;
                foreach (var value in values)
                {
                    if (value > 0)
                    {
                        total += value;
                    }
                }
                return total;
            }
        }
        """;

    /// <summary>The same shape computing in decimals, whose module imports the runtime: the lines
    /// the imports take stand above the class, and the map must move down past them.</summary>
    private const string ImportingSource = """
        namespace Demo;

        public class Ledger
        {
            public decimal Sum(decimal[] values)
            {
                decimal total = 0m;
                foreach (var value in values)
                {
                    total += value;
                }
                return total;
            }
        }
        """;

    /// <summary>Lines no C# statement writes by itself: what a strategy lowers — a pattern switch's
    /// arms, a <c>using</c>'s dispose, a <c>do</c>'s condition on the loop's last line — and the
    /// bodies the emitter built as text, an expression-bodied accessor's and an operator's. Each one
    /// read, through the map, as whatever statement was written above it.</summary>
    private const string LoweredSource = """
        using System;

        namespace Demo;

        public class Lowered
        {
            private int _level;

            public int Total { get; set; }

            public int Size => Measure(2) + 1;

            public int Level
            {
                get => _level;
                set => _level = Clamp(value);
            }

            public int Measure(int n)
            {
                int Twice(int x)
                {
                    var doubled = x * 2;
                    return doubled;
                }

                int Next(int x) => x + 1;

                n = Next(Twice(n));
                do
                {
                    n--;
                }
                while (Check(n));
                return n;
            }

            public string Name(object value)
            {
                switch (value)
                {
                    case int number when number > 0:
                        return "positive";
                    case string text:
                        return text;
                }
                return "other";
            }

            public int Scope()
            {
                using var resource = new Resource();
                return resource.Touch();
            }

            public static Lowered operator +(Lowered a, Lowered b)
            {
                var sum = new Lowered();
                sum.Total = a.Total + b.Total;
                return sum;
            }

            private bool Check(int n) => n > 0;

            private int Clamp(int value) => value < 0 ? 0 : value;
        }

        public sealed class Resource : IDisposable
        {
            public int Touch() => 1;

            public void Dispose()
            {
            }
        }
        """;

    /// <summary>Statements inside a lambda's block, in each place an arrow reaches the writer: an
    /// argument to a call the IR writes (<c>List.ForEach</c>, a method of the app's own), a hole of a
    /// LINQ template (<c>Where</c>, <c>Select</c>), a local's initializer, a <c>delegate</c>, an arrow
    /// inside an arrow, an async one, and the block the emitter adds to a concise body that declares
    /// a variable (#384). Each
    /// one mapped to the line that holds the lambda, so a frame or a breakpoint inside the block
    /// landed on the call.</summary>
    private const string LambdaSource = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;

        namespace Demo;

        public class Lambdas
        {
            public int Total(List<int> values)
            {
                var total = 0;
                values.ForEach(value =>
                {
                    var twice = Twice(value);
                    total += twice;
                });
                return total;
            }

            public int Kept(int[] values)
            {
                var kept = values.Where(value =>
                {
                    var half = value + 7;
                    return half > 9;
                });
                return kept.Count();
            }

            public int Local(int seed)
            {
                Func<int, int> step = delta =>
                {
                    var next = seed + delta;
                    return next * 3;
                };
                Action<int> report = delegate (int shown)
                {
                    seed = shown - 1;
                };
                report(step(1));
                return seed;
            }

            public int Nested(List<int> values)
            {
                var sum = 0;
                values.ForEach(outer =>
                {
                    values.ForEach(inner =>
                    {
                        sum += outer * inner;
                    });
                });
                return sum;
            }

            public async Task<int> Later(List<int> values)
            {
                var count = 0;
                Func<Task> run = async () =>
                {
                    await Task.Yield();
                    count = values.Count;
                };
                await run();
                return count;
            }

            public int Parsed(string text)
            {
                Func<string, int> parse = candidate =>
                    int.TryParse(candidate, out var number) ? number : 0;
                return parse(text);
            }

            public int Applied(int seed)
            {
                return Apply(seed, value =>
                {
                    var bumped = value + 5;
                    return bumped;
                });
            }

            public int Doubled(int[] values)
            {
                var doubled = values.Select(value =>
                {
                    var twiceOver = value * 2;
                    return twiceOver;
                });
                return doubled.Count();
            }

            public int Tail(List<int> values)
            {
                var kept = values.FindAll(value =>
                {
                    var doubled = value * 2;
                    return doubled > 0;
                }).Count + Twice(values.Count);
                return kept;
            }

            public int Guarded(bool ready, string text)
            {
                var total = 0;
                if (ready)
                    total += int.TryParse(text, out var parsed) ? parsed : 0;
                return total;
            }

            private int Apply(int seed, Func<int, int> step) => step(seed);

            private int Twice(int x) => x * 2;
        }
        """;

    /// <summary>Bodies with an <c>out</c> or a <c>ref</c> parameter (#487): a method's, whose outs come
    /// back in the object it returns, and a lambda's and a local function's, which every call site
    /// unwraps the same way. The body runs inside an arrow the lowering adds around it, and that
    /// wrapper was TEXT, so the body's statements reached the writer with no origin: the first one
    /// shared the wrapper's line, and a breakpoint on any of them bound nowhere.</summary>
    private const string ByReferenceSource = """
        namespace Demo;

        public delegate bool Probe(string text, out int length);

        public class Halves
        {
            public bool TryHalf(int value, out int half)
            {
                var doubled = value * 2;
                half = doubled / 4;
                return value % 2 == 0;
            }

            public int Swap(ref int seed)
            {
                var before = seed;
                seed = before + 3;
                return before;
            }

            public int Measured(string text)
            {
                Probe probe = (string candidate, out int length) =>
                {
                    var trimmed = candidate.Trim();
                    length = trimmed.Length;
                    return length > 0;
                };
                bool Split(string source, out int parts)
                {
                    var pieces = source.Split(',');
                    parts = pieces.Length;
                    return parts > 1;
                }
                probe(text, out var measured);
                Split(text, out var counted);
                return measured + counted;
            }
        }
        """;

    /// <summary>An iterator, whose yields push onto a buffer the lowering declares in front of the body
    /// and returns after it. The body's statements carry their own lines, and the buffer's two lines
    /// carried none, so a frame or a breakpoint on them led nowhere (#566).</summary>
    private const string IteratorSource = """
        using System.Collections.Generic;

        namespace Demo;

        public class Sequences
        {
            public IEnumerable<int> Evens(int limit)
            {
                for (var i = 0; i < limit; i++)
                {
                    var doubled = i * 2;
                    yield return doubled;
                }
            }
        }
        """;

    /// <summary>The same shape on a component, whose methods the emitter writes on its own path.</summary>
    private const string ComponentByReferenceSource = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Gauge : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                new Text(Read(3, out var scaled) ? scaled.ToString() : "", TypeRole.BodyM);

            private bool Read(int raw, out int scaled)
            {
                var widened = raw * 10;
                scaled = widened + 1;
                return scaled > 0;
            }
        }
        """;

    /// <summary>Lambdas in the two carriers that wrote what they held as TEXT (#492): an
    /// expression-bodied member, whose body was a raw return, and an object creation, whose arguments
    /// and initializer were spliced as text, each alone, and a creation inside an expression body,
    /// the shape a form rule is written in. Every line of such a lambda's block read, through the map,
    /// as the statement that held the lambda.</summary>
    private const string CarriedSource = """
        using System;
        using System.Collections.Generic;

        namespace Demo;

        public class Rule
        {
            public Rule(string message, Func<string, bool> test)
            {
                Message = message;
                Test = test;
            }

            public string Message { get; }

            public Func<string, bool> Test { get; }

            public Action<bool>? Changed { get; set; }
        }

        public class Carried
        {
            private int _seen;

            public Carried(List<int> seed) => seed.ForEach(item =>
            {
                var squared = item * item;
                _seen += squared;
            });

            public void Bump(List<int> values) => values.ForEach(value =>
            {
                var doubled = value * 2;
                _seen += doubled;
            });

            public int Size => Apply(start =>
            {
                var bumped = start + 1;
                return bumped;
            });

            public int Level
            {
                get => _seen;
                set => Apply(next =>
                {
                    var clamped = next < 0 ? 0 : next;
                    _seen = clamped + value;
                    return clamped;
                });
            }

            public Rule Built()
            {
                var rule = new Rule("built", text =>
                {
                    var length = text.Length;
                    return length > 2;
                })
                {
                    Changed = changed =>
                    {
                        var flag = changed ? 1 : 0;
                        _seen = flag;
                    },
                };
                return rule;
            }

            public static Rule Email(string message = "invalid") => new(message, address =>
            {
                var at = address.IndexOf('@');
                return at > 0;
            });

            private int Apply(Func<int, int> step) => step(_seen);
        }
        """;

    /// <summary>The carriers on a component's own paths: an expression-bodied constructor and an
    /// expression-bodied Build that constructs a node with a handler.</summary>
    private const string ComponentCarriedSource = """
        using System;
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Clicker : StatefulComponent
        {
            private int _count;

            public Clicker(int start) => Defer(() =>
            {
                var initial = start * 2;
                _count = initial;
            });

            public override VisualNode Build(ComponentContext context) => new Button("Add", onPressed: () =>
            {
                var next = _count + 1;
                SetState(() => _count = next);
            });

            private void Defer(Action run) => run();
        }
        """;

    private static CompilationResult Compile() => Compile(Source, "Tally.cs");

    private static CompilationResult Compile(string source, string path)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path));
        var tree = CSharpSyntaxTree.ParseText(source, path: path);
        var compilation = CSharpCompilation.Create("Maps", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        // The module of the type the file is named for: a source may declare a helper type beside it.
        var result = compiler.CompileSource(source, path).Single(result => result.ComponentName == Path.GetFileNameWithoutExtension(path));
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result;
    }

    /// <summary>The 0-based line of the first C# line that contains <paramref name="text"/>.</summary>
    private static int CSharpLine(string text, string source = Source) =>
        source.Split('\n').Select((line, index) => (line, index)).First(pair => pair.line.Contains(text)).index;

    [Theory]
    [InlineData("let total = 0;", "var total = 0;")]
    [InlineData("for (const value of values)", "foreach (var value in values)")]
    [InlineData("if (value > 0)", "if (value > 0)")]
    [InlineData("total += value;", "total += value;")]
    [InlineData("return total;", "return total;")]
    public void AStatement_MapsToItsOwnCSharpLine(string emitted, string written) =>
        AssertMapped(Compile(), emitted, written, Source);

    [Theory]
    [InlineData("for (const value of values)", "foreach (var value in values)")]
    [InlineData("total = total.add(value);", "total += value;")]
    [InlineData("return total;", "return total;")]
    public void AStatementUnderTheModulesImports_MapsToItsOwnCSharpLine(string emitted, string written)
    {
        var result = Compile(ImportingSource, "Ledger.cs");
        result.TypeScript.Should().StartWith("import ", "this case is about the lines the imports take");
        AssertMapped(result, emitted, written, ImportingSource);
    }

    [Theory]
    [InlineData("return this.measure(2) + 1;", "public int Size => Measure(2) + 1;")]
    [InlineData("this._level = this.clamp(value);", "set => _level = Clamp(value);")]
    [InlineData("while (this.check(n));", "while (Check(n));")]
    [InlineData("number > 0", "case int number when number > 0:")]
    [InlineData("typeof $s === 'string'", "case string text:")]
    [InlineData("const $s = value;", "switch (value)")]
    [InlineData("resource.dispose();", "using var resource = new Resource();")]
    [InlineData("sum.total = a.total + b.total;", "sum.Total = a.Total + b.Total;")]
    [InlineData("let doubled = x * 2;", "var doubled = x * 2;")]
    [InlineData("return x + 1;", "int Next(int x) => x + 1;")]
    public void ALineNoStatementWritesByItself_MapsToTheCSharpThatProducedIt(string emitted, string written) =>
        AssertMapped(Compile(LoweredSource, "Lowered.cs"), emitted, written, LoweredSource);

    [Theory]
    [InlineData("let twice = this.twice(value);", "var twice = Twice(value);")]
    [InlineData("total += twice;", "total += twice;")]
    [InlineData("let half = value + 7;", "var half = value + 7;")]
    [InlineData("return half > 9;", "return half > 9;")]
    [InlineData("let next = seed + delta;", "var next = seed + delta;")]
    [InlineData("return next * 3;", "return next * 3;")]
    [InlineData("seed = shown - 1;", "seed = shown - 1;")]
    [InlineData("sum += outer * inner;", "sum += outer * inner;")]
    [InlineData("count = values.length;", "count = values.Count;")]
    [InlineData("let bumped = value + 5;", "var bumped = value + 5;")]
    [InlineData("let twiceOver = value * 2;", "var twiceOver = value * 2;")]
    [InlineData("let number: any;", "int.TryParse(candidate, out var number) ? number : 0;")]
    [InlineData("return ((number = $eq.num.intTryParse(candidate", "int.TryParse(candidate, out var number) ? number : 0;")]
    public void AStatementInALambdasBlock_MapsToItsOwnCSharpLine(string emitted, string written) =>
        AssertMapped(Compile(LambdaSource, "Lambdas.cs"), emitted, written, LambdaSource);

    /// <summary>What follows a lambda's block on its closing line belongs to the statement that
    /// holds the lambda: nothing marked it again once the block's statements carried marks, so it
    /// read, through the map, as the block's last statement (found in review, #384). And a body C#
    /// writes without braces keeps its own line when a declaration it hoists braces it, where the
    /// braces handed both lines to the `if` around them.</summary>
    [Theory]
    [InlineData("this.twice(values.length);", "var kept = values.FindAll(value =>")]
    [InlineData("total += ((parsed", "total += int.TryParse(text, out var parsed) ? parsed : 0;")]
    public void AStatementsRestAndABracedBody_MapToTheirOwnStatement(string emitted, string written) =>
        AssertMapped(Compile(LambdaSource, "Lambdas.cs"), emitted, written, LambdaSource);

    [Theory]
    [InlineData("let doubled = value * 2;", "var doubled = value * 2;")]
    [InlineData("half = ", "half = doubled / 4;")]
    [InlineData("return value % 2 === 0;", "return value % 2 == 0;")]
    [InlineData("let before = seed;", "var before = seed;")]
    [InlineData("seed = before + 3;", "seed = before + 3;")]
    [InlineData("return before;", "return before;")]
    [InlineData("let trimmed = ", "var trimmed = candidate.Trim();")]
    [InlineData("length = trimmed.length;", "length = trimmed.Length;")]
    [InlineData("return length > 0;", "return length > 0;")]
    [InlineData("let pieces = ", "var pieces = source.Split(',');")]
    [InlineData("parts = pieces.length;", "parts = pieces.Length;")]
    [InlineData("return parts > 1;", "return parts > 1;")]
    public void AStatementOfABodyWithAnOutOrRefParameter_MapsToItsOwnCSharpLine(string emitted, string written) =>
        AssertMapped(Compile(ByReferenceSource, "Halves.cs"), emitted, written, ByReferenceSource);

    /// <summary>The lines a wrapper adds map to the declaration whose body it wraps: the arrow an out
    /// parameter's body runs in, and the buffer an iterator fills (#566).</summary>
    [Theory]
    [InlineData("const $r = ", "public bool TryHalf(int value, out int half)", "Halves.cs")]
    [InlineData("return { $: $r, half };", "public bool TryHalf(int value, out int half)", "Halves.cs")]
    [InlineData("const _seq = [];", "public IEnumerable<int> Evens(int limit)", "Sequences.cs")]
    [InlineData("return _seq;", "public IEnumerable<int> Evens(int limit)", "Sequences.cs")]
    [InlineData("let doubled = i * 2;", "var doubled = i * 2;", "Sequences.cs")]
    public void ALineAWrapperAdds_MapsToTheDeclarationWhoseBodyItWraps(string emitted, string written, string file)
    {
        var source = file == "Halves.cs" ? ByReferenceSource : IteratorSource;
        AssertMapped(Compile(source, file), emitted, written, source);
    }

    [Theory]
    [InlineData("let widened = raw * 10;", "var widened = raw * 10;")]
    [InlineData("scaled = widened + 1;", "scaled = widened + 1;")]
    [InlineData("return scaled > 0;", "return scaled > 0;")]
    public void AStatementOfAComponentsMethodWithAnOutParameter_MapsToItsOwnCSharpLine(string emitted, string written) =>
        AssertMapped(Compile(ComponentByReferenceSource, "Gauge.cs"), emitted, written, ComponentByReferenceSource);

    [Theory]
    [InlineData("let squared = item * item;", "var squared = item * item;")]
    [InlineData("this._seen += squared;", "_seen += squared;")]
    [InlineData("let doubled = value * 2;", "var doubled = value * 2;")]
    [InlineData("this._seen += doubled;", "_seen += doubled;")]
    [InlineData("let bumped = start + 1;", "var bumped = start + 1;")]
    [InlineData("return bumped;", "return bumped;")]
    [InlineData("let clamped = ", "var clamped = next < 0 ? 0 : next;")]
    [InlineData("this._seen = clamped + value;", "_seen = clamped + value;")]
    [InlineData("let length = text.length;", "var length = text.Length;")]
    [InlineData("return length > 2;", "return length > 2;")]
    [InlineData("let flag = ", "var flag = changed ? 1 : 0;")]
    [InlineData("this._seen = flag;", "_seen = flag;")]
    [InlineData("let at = ", "var at = address.IndexOf('@');")]
    [InlineData("return at > 0;", "return at > 0;")]
    public void AStatementInALambdaThatAnExpressionBodyOrACreationHolds_MapsToItsOwnCSharpLine(string emitted, string written) =>
        AssertMapped(Compile(CarriedSource, "Carried.cs"), emitted, written, CarriedSource);

    [Theory]
    [InlineData("let initial = start * 2;", "var initial = start * 2;")]
    [InlineData("this._count = initial;", "_count = initial;")]
    [InlineData("let next = this._count + 1;", "var next = _count + 1;")]
    public void AStatementInALambdaThatAComponentsExpressionBodyHolds_MapsToItsOwnCSharpLine(string emitted, string written) =>
        AssertMapped(Compile(ComponentCarriedSource, "Clicker.cs"), emitted, written, ComponentCarriedSource);

    private static void AssertMapped(CompilationResult result, string emitted, string written, string source)
    {
        result.SourceMap.Should().NotBeNullOrEmpty();
        var lines = result.TypeScript.Split('\n');
        var generatedLine = Array.FindIndex(lines, line => line.Contains(emitted));
        generatedLine.Should().BeGreaterThanOrEqualTo(0, $"the module writes `{emitted}`:\n{result.TypeScript}");

        var segments = SourceMapMappings.Decode(SourceMapMappings.Extract(result.SourceMap!));
        var column = lines[generatedLine].IndexOf(emitted, StringComparison.Ordinal);
        // What a debugger does with a position: the last segment on its line at or before its column.
        var segment = segments.ElementAtOrDefault(generatedLine)?.LastOrDefault(s => s.GeneratedColumn <= column);

        segment.Should().NotBeNull($"line {generatedLine + 1} of the module, `{lines[generatedLine].Trim()}`, carries a mapping");
        segment!.SourceLine.Should().Be(CSharpLine(written, source), $"`{emitted}` came from `{written}`");
    }
}
