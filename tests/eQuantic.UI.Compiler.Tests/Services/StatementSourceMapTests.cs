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

    private static CompilationResult Compile() => Compile(Source, "Tally.cs");

    private static CompilationResult Compile(string source, string path)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
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
    [InlineData("typeof _s === 'string'", "case string text:")]
    [InlineData("const _s = value;", "switch (value)")]
    [InlineData("resource.dispose();", "using var resource = new Resource();")]
    [InlineData("sum.total = a.total + b.total;", "sum.Total = a.Total + b.Total;")]
    [InlineData("let doubled = x * 2;", "var doubled = x * 2;")]
    [InlineData("return x + 1;", "int Next(int x) => x + 1;")]
    public void ALineNoStatementWritesByItself_MapsToTheCSharpThatProducedIt(string emitted, string written) =>
        AssertMapped(Compile(LoweredSource, "Lowered.cs"), emitted, written, LoweredSource);

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
