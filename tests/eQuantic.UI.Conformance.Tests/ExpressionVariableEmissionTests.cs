using System.Text.Json;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Every place the EMITTER writes code declares the variables its expressions declare (#466): a
/// field's and a property's initializer, a constructor, a getter and a setter, a method and a local
/// function in it, an iterator, a record's members, operator and conversion, a component's Build.
/// The statement cases (ExpressionVariableConformanceTests) run through the converter alone; these
/// run through <see cref="ComponentCompiler"/>, where each kind of member has its own path, and
/// three of those paths once declared nothing, one declared only an <c>out var</c> and one kept its
/// own copy of the rule.
/// <para>
/// One source, both sides: .NET evaluates it with the calls below, and the emitted modules run in the
/// embedded Bun in BOTH of the emitter's modes. TypeScript is bundled as a build bundles it. Plain
/// JavaScript runs as written, parsed as the JavaScript it claims to be, which is how the
/// playground, the harness and the design host consume it: there a <c>let n: any;</c> is a
/// SyntaxError that costs the whole module, and every method with an <c>out var</c> carried one.
/// </para>
/// </summary>
public class ExpressionVariableEmissionTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using eQuantic.UI.Primitives;

        public sealed class Probe
        {
            // Two initializers binding one name: each is its own C# scope.
            private readonly int _first = int.TryParse("9", out var parsed) ? parsed : 0;
            private readonly int _second = int.TryParse("8", out var parsed) ? parsed : 0;
            public int Fields => _first * 10 + _second;

            private static readonly int Shared = int.TryParse("5", out var shared) ? shared : 0;
            public static int SharedValue => Shared;

            public int Initialized { get; set; } = int.TryParse("6", out var initial) ? initial : 0;

            public int Constructed;
            public Probe() { int.TryParse("1", out var constructed); Constructed = constructed; }

            public int BlockGetter { get { int.TryParse("8", out var got); return got; } }
            public int ArrowGetter => int.TryParse("7", out var arrow) ? arrow : -1;

            private int _stored;
            public int Stored { get => _stored; set { int.TryParse(value.ToString(), out var written); _stored = written * 2; } }
            public int SetAndRead() { Stored = 21; return Stored; }

            public int LoopCapture()
            {
                var reads = new List<Func<int>>();
                foreach (var text in new[] { "1", "2" }) { int.TryParse(text, out var item); reads.Add(() => item); }
                var first = reads[0];
                var second = reads[1];
                return first() * 10 + second();
            }

            public int WhileCapture()
            {
                var reads = new List<Func<int>>();
                var i = 0;
                while (int.TryParse(i < 2 ? (i + 1).ToString() : "stop", out var item)) { reads.Add(() => item); i++; }
                var first = reads[0];
                var second = reads[1];
                return first() * 10 + second();
            }

            public int ForCapture()
            {
                var reads = new List<Func<int>>();
                for (var i = 0; i < 2 && int.TryParse((i + 1).ToString(), out var item); i++) reads.Add(() => item);
                var first = reads[0];
                var second = reads[1];
                return first() * 10 + second();
            }

            public int Recursion()
            {
                int Digits(int depth)
                {
                    int.TryParse(depth.ToString(), out var digit);
                    var rest = depth > 0 ? Digits(depth - 1) : 0;
                    return digit * 10 + rest;
                }
                return Digits(2);
            }

            public int LocalArrow()
            {
                int Unbox(object value) => value is int number ? number : 0;
                return Unbox(4) + Unbox("x");
            }

            public int Deconstruct()
            {
                (var c, var d) = (3, 4);
                var (e, (f, g)) = (5, (6, 7));
                return c + d + e + f + g;
            }

            public int Verbatim()
            {
                var @class = 5;
                int.TryParse("7", out var @new);
                return @class * 10 + @new;
            }

            public static int StaticMethod(string text) { int.TryParse(text, out var number); return number; }

            public int ExpressionBodied(string text) => int.TryParse(text, out var number) ? number : -1;

            public IEnumerable<int> Iterate() { foreach (var value in new object[] { 1, "x", 2 }) yield return value is int number ? number : 0; }
            public int Iterated() => Iterate().Sum();

            public static int Sum() => (new Money(100) + new Money(150)).Cents;
            public static int Converted() { Money money = "300"; return money.Cents; }
        }

        public readonly record struct Money(int Cents)
        {
            public static readonly Money Parsed = new(int.TryParse("250", out var cents) ? cents : 0);
            public int Doubled => int.TryParse(Cents.ToString(), out var twice) ? twice * 2 : 0;
            public int Parse(string text) { int.TryParse(text, out var number); return number + Cents; }
            public int ParseArrow(string text) => int.TryParse(text, out var number) ? number + Cents : 0;
            public static Money operator +(Money left, Money right) =>
                new(int.TryParse((left.Cents + right.Cents).ToString(), out var sum) ? sum : 0);
            public static implicit operator Money(string text) => new(int.TryParse(text, out var parsed) ? parsed : 0);
        }

        public sealed class Screen : StatelessComponent
        {
            public int Last;

            public override VisualNode Build(ComponentContext context)
            {
                int.TryParse("7", out var number);
                (var tens, var units) = (3, 4);
                Last = number * 100 + tens * 10 + units;
                return new Text("x", TypeRole.BodyM);
            }
        }
        """;

    /// <summary>Each call on both sides: the C# and the JavaScript that names the same member.</summary>
    private static readonly (string Name, string CSharp, string JavaScript)[] Calls =
    [
        ("two field initializers binding one name", "new Probe().Fields", "new Probe().fields"),
        ("a static field initializer", "Probe.SharedValue", "Probe.sharedValue"),
        ("a property initializer", "new Probe().Initialized", "new Probe().initialized"),
        ("a constructor", "new Probe().Constructed", "new Probe().constructed"),
        ("a getter's block", "new Probe().BlockGetter", "new Probe().blockGetter"),
        ("a getter's expression", "new Probe().ArrowGetter", "new Probe().arrowGetter"),
        ("a setter", "new Probe().SetAndRead()", "new Probe().setAndRead()"),
        ("a foreach body, one variable per iteration", "new Probe().LoopCapture()", "new Probe().loopCapture()"),
        ("a while condition, one variable per iteration", "new Probe().WhileCapture()", "new Probe().whileCapture()"),
        ("a for condition, one variable per iteration", "new Probe().ForCapture()", "new Probe().forCapture()"),
        ("a recursive local function, one variable per call", "new Probe().Recursion()", "new Probe().recursion()"),
        ("a local function's expression", "new Probe().LocalArrow()", "new Probe().localArrow()"),
        ("a deconstruction's elements", "new Probe().Deconstruct()", "new Probe().deconstruct()"),
        ("names JavaScript reserves", "new Probe().Verbatim()", "new Probe().verbatim()"),
        ("a static method", "Probe.StaticMethod(\"4\")", "Probe.staticMethod('4')"),
        ("a method's expression", "new Probe().ExpressionBodied(\"6\")", "new Probe().expressionBodied('6')"),
        ("an iterator", "new Probe().Iterated()", "new Probe().iterated()"),
        ("a record's static field initializer", "Money.Parsed.Cents", "Money.parsed.cents"),
        ("a record's getter", "new Money(21).Doubled", "new Money(21).doubled"),
        ("a record's method block", "new Money(1).Parse(\"5\")", "new Money(1).parse('5')"),
        ("a record's method expression", "new Money(1).ParseArrow(\"5\")", "new Money(1).parseArrow('5')"),
        ("a record's operator", "Probe.Sum()", "Probe.sum()"),
        ("a record's conversion", "Probe.Converted()", "Probe.converted()"),
        ("a component's Build",
            "((Func<int>)(() => { var screen = new Screen(); screen.Build(null!); return screen.Last; }))()",
            "(() => { const screen = new Screen(); screen.build(null); return screen.last; })()"),
    ];

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryMemberDeclaresWhatItsExpressionsDeclare(bool typeAnnotations)
    {
        var bun = JsExecutor.RequireBun();
        var expected = JsonSerializer.Deserialize<JsonElement[]>(DotNetEvaluator.EvaluateToJson(
            $"new object[] {{ {string.Join(", ", Calls.Select(call => call.CSharp))} }}", Source))!;
        var actual = JsonSerializer.Deserialize<JsonElement[]>(RunEmitted(bun, typeAnnotations))!;

        actual.Should().HaveCount(Calls.Length);
        var diverging = Calls.Select((call, i) => (call.Name, Expected: expected[i].GetRawText(), Actual: actual[i].GetRawText()))
            .Where(result => result.Expected != result.Actual)
            .Select(result => $"{result.Name}: .NET {result.Expected}, JavaScript {result.Actual}")
            .ToList();
        diverging.Should().BeEmpty($"every member of the emitted {(typeAnnotations ? "TypeScript" : "JavaScript")} answers as .NET does");
    }

    /// <summary>The emitted modules, run in Bun with every call, as one JSON array. A call that
    /// throws answers with the error, so one ReferenceError names its member instead of ending the
    /// run; a module that does not PARSE ends it, which is the failure it should be.</summary>
    private static string RunEmitted(string bun, bool typeAnnotations)
    {
        var dir = Directory.CreateTempSubdirectory("eq-expression-variables-").FullName;
        try
        {
            var modules = Compile(typeAnnotations);
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
                ModuleBundler.Bundle(bun, entries, Path.Combine(dir, "out"), written, SourceMapMode.None).Should().BeNull();
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

            var harness = Path.Combine(dir, "run.mjs");
            File.WriteAllText(harness,
                string.Concat(modules.Select(module => $"import {{ {module.ComponentName} }} from '{importDir}/{module.ComponentName}.js';\n"))
                + "const answer = (read) => { try { return read(); } catch (e) { return `threw ${e.constructor.name}: ${e.message}`; } };\n"
                + $"console.log(JSON.stringify([{string.Join(", ", Calls.Select(call => $"answer(() => {call.JavaScript})"))}]));\n");
            var (code, stdout, stderr) = JsExecutor.RunProcess(bun, ["run", harness], 30000);
            code.Should().Be(0, $"the emitted modules load and run:\n{stderr}\n{stdout}");
            return stdout.Trim();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    /// <summary>The source compiled as eqc compiles it: from a file, with a real compilation behind
    /// the model and the dependency resolver scanning the source directory, so a module imports the
    /// others it names — one module per type.</summary>
    private static List<CompilationResult> Compile(bool typeAnnotations)
    {
        var dir = Directory.CreateTempSubdirectory("eq-expression-variables-src-").FullName;
        try
        {
            var path = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(path, Source);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(file => (MetadataReference)MetadataReference.CreateFromFile(file))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
            var tree = CSharpSyntaxTree.ParseText(Source, ParseDefaults.Options, path: path);
            var compilation = CSharpCompilation.Create("ExpressionVariables", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler { TypeAnnotations = typeAnnotations };
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var modules = compiler.CompileFile(path).Where(result => result.TypeScript.Length > 0).ToList();
            Assert.All(modules, module => Assert.True(module.Success,
                $"{module.ComponentName}: {string.Join("\n", module.Errors.Select(e => e.Message))}"));
            modules.Select(module => module.ComponentName).Should().BeEquivalentTo(["Probe", "Money", "Screen"]);
            return modules;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }
}
