using System.Text.Json;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// A source run on both sides AS AN APP'S BUILD RUNS IT: eqc compiles the file with a real compilation
/// behind the model and the dependency resolver scanning its directory, one module per type, and each
/// module imports the others it names. The statement harness (<see cref="ConformanceRunner"/>) emits a
/// prelude's records into one program and never a class, so it cannot see a module that is missing, an
/// import that is, or a class's twin at all.
/// <para>
/// Every case is a block of statements returning a value. They become the static methods of one class,
/// <c>ConformanceCases</c>, compiled with the source, so the code that constructs and reads the types is
/// eqc's own translation, in a module that imports them. .NET evaluates the same source and calls each
/// method. A case that throws answers <c>"threw"</c> on either side, so one failure names its case
/// instead of ending the run.
/// </para>
/// </summary>
public static class ModuleGraph
{
    private const string Cases = "ConformanceCases";

    /// <summary>The framework and the vocabulary, read once for every compilation (#481).</summary>
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => TestReferences.Of(file))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .ToArray());

    /// <summary>
    /// Runs <paramref name="cases"/> over <paramref name="source"/> on both sides and asserts every one
    /// answers alike, in the SDK's TypeScript bundled as a build bundles it, or in plain JavaScript run as
    /// written (the playground's mode).
    /// </summary>
    public static void AssertSameAsDotNet(string source, bool typeAnnotations,
        params (string Name, string Statements)[] cases)
    {
        var bun = JsExecutor.RequireBun();
        var withCases = source + "\n" + CasesClass(cases);

        var expected = JsonSerializer.Deserialize<JsonElement[]>(DotNetEvaluator.EvaluateToJson(
            $"new object[] {{ {string.Join(", ", cases.Select((_, i) => $"ConformanceAnswers.Of(() => {Cases}.Case{i}())"))} }}",
            withCases + "\n" + AnswersClass))!;

        var (actualJson, modules) = Run(bun, withCases, cases.Length, typeAnnotations);
        var actual = JsonSerializer.Deserialize<JsonElement[]>(actualJson)!;
        actual.Should().HaveCount(cases.Length);

        var diverging = cases.Select((c, i) => (c.Name, Expected: expected[i].GetRawText(), Actual: actual[i].GetRawText()))
            .Where(result => result.Expected != result.Actual)
            .Select(result => $"{result.Name}: .NET {result.Expected}, JavaScript {result.Actual}")
            .ToList();
        diverging.Should().BeEmpty(
            $"every case answers as .NET does in the emitted {(typeAnnotations ? "TypeScript" : "JavaScript")}. The modules:\n"
            + string.Join("\n", modules.Select(module => $"// {module.ComponentName}\n{module.TypeScript}")));
    }

    /// <summary>The cases as the static class both sides compile.</summary>
    private static string CasesClass(IReadOnlyList<(string Name, string Statements)> cases) =>
        $"public static class {Cases}\n{{\n"
        + string.Concat(cases.Select((c, i) => $"    public static object Case{i}()\n    {{\n        {c.Statements}\n    }}\n"))
        + "}\n";

    /// <summary>The .NET side's catch, kept out of the source eqc compiles.</summary>
    private const string AnswersClass = """
        public static class ConformanceAnswers
        {
            public static object Of(System.Func<object> read)
            {
                try { return read(); }
                catch (System.Exception) { return "threw"; }
            }
        }
        """;

    private static (string Json, List<CompilationResult> Modules) Run(string bun, string source, int count, bool typeAnnotations)
    {
        var dir = Directory.CreateTempSubdirectory("eq-module-graph-").FullName;
        try
        {
            var modules = Compile(source, Path.Combine(dir, "src"), typeAnnotations);
            var written = Path.Combine(dir, "emitted");
            Directory.CreateDirectory(written);
            string importDir;
            if (typeAnnotations)
            {
                // TypeScript goes through the build's own bundling, flags included.
                var entries = modules.Select(module =>
                {
                    var path = Path.Combine(written, $"{module.ComponentName}.ts");
                    File.WriteAllText(path, module.TypeScript);
                    return path;
                }).ToList();
                var bundled = ModuleBundler.Bundle(bun, entries, Path.Combine(dir, "out"), written, SourceMapMode.None);
                bundled.Should().BeNull($"the emitted modules bundle:\n{bundled}");
                importDir = "./out";
            }
            else
            {
                // Plain JavaScript runs as written: nothing strips a type annotation on the way.
                foreach (var module in modules)
                    File.WriteAllText(Path.Combine(written, $"{module.ComponentName}.js"), module.TypeScript);
                importDir = "./emitted";
            }

            // The runtime a module imports by its bare name, resolved the way a page's import map does.
            var shim = Path.Combine(dir, "node_modules", "@equantic", "runtime");
            Directory.CreateDirectory(shim);
            File.WriteAllText(Path.Combine(shim, "package.json"), """{ "name": "@equantic/runtime", "type": "module", "main": "index.js" }""");
            File.WriteAllText(Path.Combine(shim, "index.js"), $"export * from '{ConformanceRunner.RuntimeJsUrl()}';\n");

            var driver = Path.Combine(dir, "run.mjs");
            File.WriteAllText(driver,
                $"import {{ {Cases} }} from '{importDir}/{Cases}.js';\n"
                + "const answer = (read) => { try { const value = read(); return value === undefined ? null : value; } catch (e) { return 'threw'; } };\n"
                + $"console.log(JSON.stringify([{string.Join(", ", Enumerable.Range(0, count).Select(i => $"answer(() => {Cases}.case{i}())"))}]));\n");
            var (code, stdout, stderr) = JsExecutor.RunProcess(bun, ["run", driver], 30000);
            code.Should().Be(0, $"the emitted modules load and run:\n{stderr}\n{stdout}\n"
                + string.Join("\n", modules.Select(module => $"// {module.ComponentName}\n{module.TypeScript}")));
            return (stdout.Trim(), modules);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>The source compiled as eqc compiles an app's file: from disk, with a real compilation
    /// behind the model and the resolver scanning the directory, so a module imports what it names.</summary>
    private static List<CompilationResult> Compile(string source, string dir, bool typeAnnotations)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Probe.cs");
        File.WriteAllText(path, source);
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: path);
        var compilation = CSharpCompilation.Create("ModuleGraph", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())
            .Should().BeEmpty("the case is C# that compiles");

        var resolver = new ComponentDependencyResolver();
        resolver.ScanSourceDirectories([dir]);
        var compiler = new ComponentCompiler { TypeAnnotations = typeAnnotations };
        compiler.SetProjectCompilation(compilation);
        compiler.SetDependencyResolver(resolver);
        var modules = compiler.CompileFile(path).Where(result => result.TypeScript.Length > 0).ToList();
        modules.Where(module => !module.Success)
            .Select(module => $"{module.ComponentName}: {string.Join("\n", module.Errors.Select(e => $"{e.Code} {e.Message}"))}")
            .Should().BeEmpty("every module of the source compiles");
        return modules;
    }
}
