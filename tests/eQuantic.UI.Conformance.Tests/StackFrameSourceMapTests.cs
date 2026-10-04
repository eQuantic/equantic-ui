using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A thrown error's frames lead back to the C# lines that threw and called (#293), through the
/// pipeline a build runs: eqc's TypeScript and its map, bundled by <see cref="ModuleBundler"/> with
/// the embedded Bun and the build's own flags, and the two maps composed. The JavaScript runs in Bun
/// WITHOUT its map comment, so the stack carries raw positions, and the test reads them through the
/// composed map the way a browser's debugger does. Before statement-level mappings, every position
/// inside a body traced to the method's first line. The call is kept out of tail position, even
/// after the minifier folds the local into the return: Bun's engine drops the caller's frame for a
/// strict-mode tail call, as the language allows.
/// </summary>
public class StackFrameSourceMapTests
{
    private const string Source = """
        using System;

        namespace Demo;

        public class Thrower
        {
            public int Outer(int x)
            {
                var doubled = x * 2;
                var result = Run(doubled);
                return result + 1;
            }

            public int Run(int x)
            {
                var y = x + 1;
                if (y > 0)
                {
                    throw new InvalidOperationException("boom");
                }
                return y;
            }
        }
        """;

    private const string ComponentSource = """
        using System;
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Boom : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text(Describe(1), TypeRole.BodyM);

            public string Describe(int count)
            {
                var doubled = count * 2;
                if (doubled > 1)
                {
                    throw new InvalidOperationException("too many");
                }
                return doubled.ToString();
            }

            public string Twice(int count)
            {
                var text = Describe(count);
                return text + text;
            }
        }
        """;

    /// <summary>Frames on lines no C# statement writes by itself: a <c>do</c>'s condition, on the
    /// loop's last line, and an expression-bodied getter's return, which the emitter built as text.
    /// The getter's sum keeps the call out of tail position, as above.</summary>
    private const string LoweredSource = """
        using System;

        namespace Demo;

        public class Countdown
        {
            public int Size => Count(3) + 1;

            public int Count(int n)
            {
                do
                {
                    n--;
                }
                while (Check(n));
                return n;
            }

            public bool Check(int n)
            {
                if (n < 2)
                {
                    throw new InvalidOperationException("low");
                }
                return true;
            }
        }
        """;

    /// <summary>A frame inside a lambda's block (#384): it read, through the map, as the line that
    /// holds the lambda, whatever statement in the block had called. The sum keeps the call out of
    /// tail position, as above.</summary>
    private const string LambdaSource = """
        using System;
        using System.Collections.Generic;

        namespace Demo;

        public class Walker
        {
            public int Walk(int count)
            {
                var total = 0;
                var values = new List<int> { count };
                values.ForEach(value =>
                {
                    var seen = value * 10;
                    total += Check(seen) + 1;
                });
                return total;
            }

            public int Check(int value)
            {
                if (value > 0)
                {
                    throw new InvalidOperationException("big");
                }
                return value;
            }
        }
        """;

    /// <summary>A frame in what follows a lambda's block, on its closing line: the statement that
    /// holds the lambda, which the frame read as on main, and not the block's last statement, which
    /// it read as once the block's statements carried marks and nothing marked the rest (found in
    /// review, #384). The sum keeps the call out of tail position, as above.</summary>
    private const string TailSource = """
        using System;
        using System.Collections.Generic;

        namespace Demo;

        public class Tail
        {
            public int Run(int count)
            {
                var values = new List<int> { count };
                var kept = values.FindAll(value =>
                {
                    var doubled = value * 2;
                    return doubled > 0;
                }).Count + Check(count);
                return kept;
            }

            public int Check(int value)
            {
                if (value > 0)
                {
                    throw new InvalidOperationException("tail");
                }
                return value;
            }
        }
        """;

    /// <summary>Strings holding U+2028 and U+2029 before the call (#491): JavaScript reads either as a
    /// line break when it counts positions, Bun among them, and eqc's map counts only line feeds, so a
    /// raw one moved every mapping after it a line down. String literals escaped them since #412; an
    /// interpolated string's text kept them raw until #520. The sum keeps the call out of tail
    /// position, as above.</summary>
    private const string SeparatorSource = """
        using System;

        namespace Demo;

        public class Separated
        {
            public int Run(int count)
            {
                var plain = "a\u2028b";
                var text = $"{plain}\u2029{count}\u2028";
                var result = Check(text.Length + count);
                return result + 1;
            }

            public int Check(int value)
            {
                if (value > 0)
                {
                    throw new InvalidOperationException("separated");
                }
                return value;
            }
        }
        """;

    /// <summary>A frame inside a body with an <c>out</c> parameter (#487), which runs in an arrow the
    /// lowering adds around it: the throw's line, and the caller's. The wrapper was text, so the body
    /// had no segment of its own and the throw read as the method's head, whatever was mapped before
    /// it on the bundled line. The arrow's call is a frame C# does not have, and the bundler's map
    /// leads its position into the arrow's last statement, so it is not asked about.</summary>
    private const string ByReferenceSource = """
        using System;

        namespace Demo;

        public class Splitter
        {
            public int Run(int count)
            {
                var ok = TryHalf(count, out var half);
                return (ok ? half : 0) + 1;
            }

            public bool TryHalf(int value, out int half)
            {
                var doubled = value * 2;
                if (doubled > 0)
                {
                    throw new InvalidOperationException("odd");
                }
                half = doubled / 4;
                return true;
            }
        }
        """;

    /// <summary>A frame inside a default an interface supplies (#490), whose body is written into the
    /// class's module from the interface's file: it read as a line of the class's file, which that
    /// file does not have. The sum keeps the call out of tail position, as above.</summary>
    private const string DefaultInterfaceSource = """
        using System;

        namespace Demo;

        public interface IChecked
        {
            int Check(int value)
            {
                var limit = value * 2;
                if (limit > 0)
                {
                    throw new InvalidOperationException("default");
                }
                return limit;
            }
        }
        """;

    private const string DefaultClassSource = """
        namespace Demo;

        public class Checked : IChecked
        {
            public int Run(int count)
            {
                var result = ((IChecked)this).Check(count);
                return result + 1;
            }
        }
        """;

    /// <summary>Frames inside lambdas that an expression-bodied member and an object creation hold
    /// (#492): both carriers wrote the lambda as text, so its lines read as the statement holding it.
    /// The sums keep the calls out of tail position, as above.</summary>
    private const string CarriedSource = """
        using System;

        namespace Demo;

        public class Step
        {
            public Step(Func<int, int> run)
            {
                Run = run;
            }

            public Func<int, int> Run { get; }
        }

        public class Holder
        {
            public int Bodied(int count) => Apply(count, value =>
            {
                var seen = value * 10;
                return Check(seen) + 1;
            });

            public int Created(int count)
            {
                var step = new Step(value =>
                {
                    var seen = value * 100;
                    return Check(seen) + 1;
                });
                return step.Run(count) + 1;
            }

            private int Apply(int seed, Func<int, int> step) => step(seed) + 1;

            public int Check(int value)
            {
                if (value > 0)
                {
                    throw new InvalidOperationException("carried");
                }
                return value;
            }
        }
        """;

    /// <summary>The 1-based line of the first line of <paramref name="source"/> that contains <paramref name="text"/>.</summary>
    private static int LineOf(string source, string text) =>
        source.Split('\n').Select((line, index) => (line, index)).First(pair => pair.line.Contains(text)).index + 1;

    [SkippableFact]
    public void AThrownErrorsFrames_LeadToTheCSharpLinesThatThrewAndCalled()
    {
        var (stack, frames, map) = Throw(Source, "Thrower", "new Thrower().outer(1)");
        Resolve(map, frames[0]).Should().Be(("Thrower.cs", LineOf(Source, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Thrower.cs", LineOf(Source, "var result = Run(doubled);")), $"the next frame is the call:\n{stack}");
    }

    /// <summary>The same from a component, whose methods the emitter writes on another path, and
    /// whose module imports the runtime by its bare name.</summary>
    [SkippableFact]
    public void AComponentsThrownErrorsFrames_LeadToItsCSharpLines()
    {
        var (stack, frames, map) = Throw(ComponentSource, "Boom", "new Boom().twice(1)");
        Resolve(map, frames[0]).Should().Be(("Boom.cs", LineOf(ComponentSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Boom.cs", LineOf(ComponentSource, "var text = Describe(count);")), $"the next frame is the call:\n{stack}");
    }

    [SkippableFact]
    public void AFrameOnALineNoStatementWritesByItself_LeadsToTheCSharpThatProducedIt()
    {
        var (stack, frames, map) = Throw(LoweredSource, "Countdown", "new Countdown().size");
        frames.Should().HaveCountGreaterThanOrEqualTo(3, $"the throw, the loop and the getter:\n{stack}");
        Resolve(map, frames[0]).Should().Be(("Countdown.cs", LineOf(LoweredSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Countdown.cs", LineOf(LoweredSource, "while (Check(n));")), $"the loop's condition called it:\n{stack}");
        Resolve(map, frames[2]).Should().Be(("Countdown.cs", LineOf(LoweredSource, "public int Size => Count(3) + 1;")), $"the getter called the loop:\n{stack}");
    }

    [SkippableFact]
    public void AFrameInsideALambdasBlock_LeadsToTheStatementThatCalled()
    {
        var (stack, frames, map) = Throw(LambdaSource, "Walker", "new Walker().walk(1)");
        Resolve(map, frames[0]).Should().Be(("Walker.cs", LineOf(LambdaSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Walker.cs", LineOf(LambdaSource, "total += Check(seen) + 1;")), $"the lambda's statement called it:\n{stack}");
    }

    [SkippableFact]
    public void AFrameAfterALambdasBlock_LeadsToTheStatementThatHoldsIt()
    {
        var (stack, frames, map) = Throw(TailSource, "Tail", "new Tail().run(1)");
        Resolve(map, frames[0]).Should().Be(("Tail.cs", LineOf(TailSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Tail.cs", LineOf(TailSource, "var kept = values.FindAll(value =>")), $"the statement's rest called it:\n{stack}");
    }

    [SkippableFact]
    public void AFrameAfterAStringHoldingALineSeparator_LeadsToItsOwnLine()
    {
        var (stack, frames, map) = Throw(SeparatorSource, "Separated", "new Separated().run(1)");
        Resolve(map, frames[0]).Should().Be(("Separated.cs", LineOf(SeparatorSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Separated.cs", LineOf(SeparatorSource, "var result = Check(text.Length + count);")), $"the next frame is the call, after the strings:\n{stack}");
    }

    [SkippableFact]
    public void AFrameInsideABodyWithAnOutParameter_LeadsToItsOwnLine()
    {
        var (stack, frames, map) = Throw(ByReferenceSource, "Splitter", "new Splitter().run(1)");
        var resolved = frames.Select(frame => Resolve(map, frame)).ToList();
        resolved[0].Should().Be(("Splitter.cs", LineOf(ByReferenceSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        resolved.Should().Contain(("Splitter.cs", LineOf(ByReferenceSource, "var ok = TryHalf(count, out var half);")),
            $"and the caller called the method:\n{stack}");
    }

    [SkippableFact]
    public void AFrameInsideADefaultAnInterfaceSupplies_LeadsToTheInterfacesFile()
    {
        var (stack, frames, map) = Throw(DefaultClassSource, "Checked", "new Checked().run(1)", ("IChecked.cs", DefaultInterfaceSource));
        Resolve(map, frames[0]).Should().Be(("IChecked.cs", LineOf(DefaultInterfaceSource, "throw new InvalidOperationException")), $"the top frame is the throw, in the interface's file:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Checked.cs", LineOf(DefaultClassSource, "var result = ((IChecked)this).Check(count);")), $"the class called it:\n{stack}");
    }

    [SkippableFact]
    public void AFrameInsideALambdaAnExpressionBodyHolds_LeadsToTheStatementThatCalled()
    {
        var (stack, frames, map) = Throw(CarriedSource, "Holder", "new Holder().bodied(1)");
        Resolve(map, frames[0]).Should().Be(("Holder.cs", LineOf(CarriedSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Holder.cs", LineOf(CarriedSource, "return Check(seen) + 1;")), $"the lambda's statement called it:\n{stack}");
    }

    [SkippableFact]
    public void AFrameInsideALambdaACreationHolds_LeadsToTheStatementThatCalled()
    {
        var (stack, frames, map) = Throw(CarriedSource, "Holder", "new Holder().created(1)");
        var called = CarriedSource.Split('\n').Select((line, index) => (line, index))
            .Where(pair => pair.line.Contains("return Check(seen) + 1;")).Select(pair => pair.index + 1).Last();
        Resolve(map, frames[0]).Should().Be(("Holder.cs", LineOf(CarriedSource, "throw new InvalidOperationException")), $"the top frame is the throw:\n{stack}");
        Resolve(map, frames[1]).Should().Be(("Holder.cs", called), $"the lambda's statement called it:\n{stack}");
    }

    /// <summary>
    /// Compiles <paramref name="source"/> as eqc does, with <paramref name="others"/> beside it in the
    /// compilation, bundles it as a build does, runs <paramref name="call"/> in Bun and answers the
    /// stack, the module's frames in it, and the composed map they read through.
    /// </summary>
    private static (string Stack, List<(int Line, int Column)> Frames, string Map) Throw(string source, string module, string call,
        params (string Path, string Text)[] others)
    {
        var bun = JsExecutor.RequireBun();
        var dir = Directory.CreateTempSubdirectory("eq-stack-").FullName;
        try
        {
            // eqc: the TypeScript, its map, and the comment that leads bun to it.
            // Every module the source compiles to, so one that constructs a type declared beside it
            // finds that type's module where a build writes it.
            var tsDir = Path.Combine(dir, "ts");
            var outDir = Path.Combine(dir, "out");
            Directory.CreateDirectory(tsDir);
            foreach (var result in Compile(source, $"{module}.cs", others))
            {
                var written = Path.Combine(tsDir, $"{result.ComponentName}.ts");
                File.WriteAllText(written, result.TypeScript + $"\n//# sourceMappingURL={result.ComponentName}.ts.map");
                File.WriteAllText(written + ".map", result.SourceMap);
            }
            var tsPath = Path.Combine(tsDir, $"{module}.ts");

            // The build's bundling, flags and composition included.
            ModuleBundler.Bundle(bun, [tsPath], outDir, tsDir, SourceMapMode.Full).Should().BeNull();

            // The runtime the module imports by its bare name, resolved the way a page's import map does.
            var shim = Path.Combine(dir, "node_modules", "@equantic", "runtime");
            Directory.CreateDirectory(shim);
            File.WriteAllText(Path.Combine(shim, "package.json"), """{ "name": "@equantic/runtime", "type": "module", "main": "index.js" }""");
            File.WriteAllText(Path.Combine(shim, "index.js"), $"export * from '{ConformanceRunner.RuntimeJsUrl()}';\n");

            // No map comment: Bun must not rewrite the frames, so the positions are the JavaScript's.
            var jsPath = Path.Combine(outDir, $"{module}.js");
            File.WriteAllText(jsPath, Regex.Replace(File.ReadAllText(jsPath), @"//# sourceMappingURL=.*", ""));
            var harness = Path.Combine(dir, "run.mjs");
            File.WriteAllText(harness,
                $"import {{ {module} }} from './out/{module}.js';\n" +
                $"try {{ {call}; console.log('no throw'); }} catch (e) {{ console.log(e.stack); }}\n");
            var (_, stdout, stderr) = JsExecutor.RunProcess(bun, ["run", harness], 30000);
            var stack = stdout + stderr;

            var frames = Regex.Matches(stack, $@"{module}\.js:(\d+):(\d+)")
                .Select(m => (Line: int.Parse(m.Groups[1].Value), Column: int.Parse(m.Groups[2].Value))).ToList();
            frames.Should().HaveCountGreaterThanOrEqualTo(2, $"the stack names the module for the throw and its caller:\n{stack}");
            return (stack, frames, File.ReadAllText(jsPath + ".map"));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static IReadOnlyList<CompilationResult> Compile(string source, string path, (string Path, string Text)[] others)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (MetadataReference)TestReferences.Of(file))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var trees = others.Select(other => CSharpSyntaxTree.ParseText(other.Text, path: other.Path))
            .Prepend(CSharpSyntaxTree.ParseText(source, path: path));
        var compilation = CSharpCompilation.Create("Stack", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
        var compiler = new ComponentCompiler { SourceMaps = SourceMapMode.Full };
        compiler.SetProjectCompilation(compilation);
        // The app's own modules, as eqc scans them, so a module imports the type it constructs.
        compiler.SetDependencyResolver(ComponentDependencyResolver.From(compilation));
        var results = compiler.CompileSource(source, path).ToList();
        foreach (var result in results)
        {
            Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
            result.SourceMap.Should().NotBeNullOrEmpty();
        }
        results.Should().Contain(result => $"{result.ComponentName}.cs" == path, "the file is named for the type the test throws in");
        return results;
    }

    /// <summary>
    /// A 1-based frame position read through a v3 map as a debugger reads it: the last segment on
    /// the frame's line at or before its column, and the source and 1-based line it names.
    /// </summary>
    private static (string Source, int Line) Resolve(string mapJson, (int Line, int Column) frame)
    {
        using var map = JsonDocument.Parse(mapJson);
        var sources = map.RootElement.GetProperty("sources").EnumerateArray().Select(s => s.GetString()!).ToArray();
        var lines = map.RootElement.GetProperty("mappings").GetString()!.Split(';');
        int source = 0, sourceLine = 0;
        (int Column, int Source, int Line)? found = null;
        for (var index = 0; index < lines.Length && index < frame.Line; index++)
        {
            var column = 0;
            if (lines[index].Length == 0) continue;
            foreach (var segment in lines[index].Split(','))
            {
                var values = Vlq(segment);
                column += values[0];
                if (values.Count < 4) continue;
                source += values[1];
                sourceLine += values[2];
                if (index == frame.Line - 1 && column <= frame.Column - 1) found = (column, source, sourceLine);
            }
        }
        found.Should().NotBeNull($"the map has a segment at or before {frame.Line}:{frame.Column}");
        return (Path.GetFileName(sources[found!.Value.Source]), found.Value.Line + 1);
    }

    private static List<int> Vlq(string segment)
    {
        const string digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var values = new List<int>();
        int shift = 0, value = 0;
        foreach (var c in segment)
        {
            var digit = digits.IndexOf(c);
            value += (digit & 0x1F) << shift;
            if ((digit & 0x20) != 0)
            {
                shift += 5;
                continue;
            }
            values.Add((value & 1) == 1 ? -(value >> 1) : value >> 1);
            shift = 0;
            value = 0;
        }
        return values;
    }
}
