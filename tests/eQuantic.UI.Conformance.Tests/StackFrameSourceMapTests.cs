using System.Diagnostics;
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

    /// <summary>
    /// Compiles <paramref name="source"/> as eqc does, bundles it as a build does, runs
    /// <paramref name="call"/> in Bun and answers the stack, the module's frames in it, and the
    /// composed map they read through.
    /// </summary>
    private static (string Stack, List<(int Line, int Column)> Frames, string Map) Throw(string source, string module, string call)
    {
        Skip.If(JsExecutor.BunExecutable is null, "The embedded Bun does not run here.");
        var bun = JsExecutor.BunExecutable!;
        var dir = Directory.CreateTempSubdirectory("eq-stack-").FullName;
        try
        {
            // eqc: the TypeScript, its map, and the comment that leads bun to it.
            var result = Compile(source, $"{module}.cs");
            var tsDir = Path.Combine(dir, "ts");
            var outDir = Path.Combine(dir, "out");
            Directory.CreateDirectory(tsDir);
            var tsPath = Path.Combine(tsDir, $"{module}.ts");
            File.WriteAllText(tsPath, result.TypeScript + $"\n//# sourceMappingURL={module}.ts.map");
            File.WriteAllText(tsPath + ".map", result.SourceMap);

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
            var stack = Run(bun, harness, dir);

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

    private static CompilationResult Compile(string source, string path)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (MetadataReference)MetadataReference.CreateFromFile(file))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var tree = CSharpSyntaxTree.ParseText(source, path: path);
        var compilation = CSharpCompilation.Create("Stack", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
        var compiler = new ComponentCompiler { SourceMaps = SourceMapMode.Full };
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, path).Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        result.SourceMap.Should().NotBeNullOrEmpty();
        return result;
    }

    private static string Run(string bun, string script, string workingDirectory)
    {
        var start = new ProcessStartInfo(bun)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add(script);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30000).Should().BeTrue("the harness finishes");
        return output.Result + error.Result;
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
