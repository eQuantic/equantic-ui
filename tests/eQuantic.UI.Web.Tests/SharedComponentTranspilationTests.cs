using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Transpiles the REAL shared component sources (<c>eQuantic.UI.Components</c>) with the real
/// compiler — the same pipeline an app build runs — and pins the emitted modules committed at
/// <c>src/eQuantic.UI.Runtime/src/shared/components/</c>, where the runtime's vitest suite EXECUTES
/// them against the vocabulary classes and the generated theme (the write-once proof on web: C#
/// source → eqc → JS → the same DOM the C# WebRealizer produces). The three app-shaped fixtures sit
/// in <c>shared/__fixtures__/</c> instead, keeping the package import that is the thing they prove.
/// Refresh both with <c>EQ_UPDATE_TRANSPILED=1</c> after compiler or component changes.
/// </summary>
public class SharedComponentTranspilationTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>
    /// The shared library IS its directory. A hand-kept roster here let a new component transpile,
    /// never reach runtime.js, and fail only in the browser with "does not provide an export named
    /// X" — so the set is discovered, and adding a component to the library is the whole ceremony.
    /// </summary>
    private static string[] SharedSources() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Components"), "*.cs")
            // The chart library is the second shared library, runtime-provided the same way
            // (docs/CHARTS-PLAN.md): its directory IS its roster too.
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Charts"), "*.cs"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Pure MODEL that the components are written against and that has no platform in it — the code
    /// editor's document, tokenizers, and history. Hand-writing a TypeScript twin of a tokenizer is
    /// exactly the thing this framework exists not to do, so it transpiles with everything else and
    /// the same emission runs on both targets.
    /// </summary>
    private static string[] SharedModelSources() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Primitives", "Code"), "*.cs")
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Primitives", "Sheet"), "*.cs"))
            .Concat(Directory.GetFiles(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Primitives", "Forms"), "*.cs"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();


    /// <summary>
    /// The stateful write-once proof — the SAME authoring shape as the native CounterAppTests
    /// component (fields + SetState + Build), composing the shared Button with a NAMED argument.
    /// Kept as an in-test source: it is a demo/proof, not library surface.
    /// </summary>
    private const string SharedCounterSource = """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace eQuantic.UI.Web.Tests.Fixtures;

        public sealed class SharedCounter : StatefulComponent
        {
            private int _count;

            // The C# event-handler idiom (hover intent timers): async is a MODIFIER — the emitted
            // method must carry `async` or its awaits are bundle-time syntax errors.
            private async void BumpSoon()
            {
                await Task.Delay(140);
                SetState(() => _count++);
            }

            // Iterator methods are JS generators — `yield` must land inside a `*method`.
            private IEnumerable<VisualNode> Cells()
            {
                yield return new Text("a", TypeRole.Caption);
                if (_count > 3) yield break;
                yield return new Text("b", TypeRole.Caption);
            }

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: Space.S3);
                column.Add(new Text($"Count: {_count}", TypeRole.Title));
                foreach (var cell in Cells()) column.Add(cell);
                column.Add(new Button("Increment", onPressed: () => SetState(() => _count++)));
                column.Add(new Button("Later", onPressed: BumpSoon));
                return column;
            }
        }
        """;

    /// <summary>
    /// The positional-reconciler proof (plan W6 slice 2) — the SAME scenario as the native
    /// ReconcilerTests: a stateful child nested inside a stateful host. The host's SetState rebuilds
    /// its tree with a FRESH child every pass; the web instance store must retain the child by
    /// position (state survives) while <c>AdoptConfig</c> carries the fresh configuration over.
    /// </summary>
    private const string NestedReconcilerSource = """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace eQuantic.UI.Web.Tests.Fixtures;

        public sealed class NestedChild : StatefulComponent
        {
            private int _count;
            private string _label;

            public NestedChild(string label = "child")
            {
                _label = label;
            }

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: Space.S2);
                column.Add(new Text($"{_label}:{_count}", TypeRole.Caption));
                column.Add(new Button("Add", onPressed: () => SetState(() => _count++)));
                return column;
            }

            public override void AdoptConfig(UiComponent next)
            {
                if (next is NestedChild fresh) _label = fresh._label;
            }
        }

        public sealed class NestedHost : StatefulComponent
        {
            private int _generation;

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: Space.S2);
                column.Add(new Button("Bump", onPressed: () => SetState(() => _generation++)));
                column.Add(new NestedChild(label: $"g{_generation}"));
                return column;
            }
        }
        """;

    private static Dictionary<string, string> TranspileSharedComponents()
    {
        var root = RepoRoot();
        var sourcePaths = SharedModelSources()
            .Concat(SharedSources())
            .ToList();

        // The same semantic setup the SDK gives eqc (full resolved references — the SDK passes
        // @(ReferencePathWithRefAssemblies)): here, the test host's trusted platform assemblies,
        // which already include Primitives and eQuantic.UI.Components via project references. Minimal
        // hand-picked refs are NOT equivalent — delegate facades go missing and constructor overloads
        // silently fail to bind (breaking, e.g., named-argument reordering).
        var counterPath = Path.Combine(root, "tests", "eQuantic.UI.Web.Tests", "Fixtures", "SharedCounter.cs");
        var nestedPath = Path.Combine(root, "tests", "eQuantic.UI.Web.Tests", "Fixtures", "NestedReconciler.cs");
        var trees = sourcePaths
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .ToList();
        trees.Add(CSharpSyntaxTree.ParseText(SharedCounterSource, path: counterPath));
        trees.Add(CSharpSyntaxTree.ParseText(NestedReconcilerSource, path: nestedPath));
        // The shared sources build with <ImplicitUsings> — mirror the generated global usings, or
        // `Action?` fails to bind (CS0246) and the semantic paths silently degrade.
        trees.Add(CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
            path: "GlobalUsings.g.cs"));
        // Full TPA references, INCLUDING eQuantic.UI.Web.Components: since the merge the legacy
        // web set lives outside the eQuantic.UI.Components namespace chain, so the old
        // enclosing-namespace rebinding gotcha is structurally impossible.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create("SharedComponents", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // NOT authoritative, deliberately: this pipeline transpiles the LIBRARY's own sources with
        // the library's dll among the references, so the file being compiled is source while its
        // dependencies resolve from metadata — a `theme.Code(token.Kind)` whose argument is the
        // source-tree type can never bind against the dll's overload. The model is structurally
        // incomplete here; heuristics stay legal, and the byte-pins below are what guard emission.
        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(compilation);

        var modules = new Dictionary<string, string>();
        var compilations = sourcePaths
            .Select(path => compiler.CompileFile(path))
            .Append(compiler.CompileSource(SharedCounterSource, counterPath))
            .Append(compiler.CompileSource(NestedReconcilerSource, nestedPath));
        foreach (var result in compilations.SelectMany(results => results))
        {
            result.Success.Should().BeTrue(
                $"{result.ComponentName} must transpile cleanly: " +
                string.Join("; ", result.Errors.Select(e => e.Message)));
            modules[result.ComponentName] = result.TypeScript;
        }
        return modules;
    }

    /// <summary>The in-test proofs: they are transpiled and executed, but they are not library
    /// surface and must not be embedded in the runtime a consumer ships.</summary>
    private static readonly string[] Fixtures = ["SharedCounter", "NestedChild", "NestedHost"];

    /// <summary>The source this asks about, kept HERE rather than pointed at a production class.
    /// <para>
    /// It used to read `ButtonStyles.cs`, and that class is gone — its `Metrics` was a tuple view of
    /// seven `Sizing` rungs and its one number moved to the ladder. The rule it was proving is not
    /// about that class and outlived it, so the subject is a source of this test's own: no
    /// production file has to keep carrying an attribute for the compiler's behaviour to stay
    /// asserted, and today no shared component carries it at all.
    /// </para></summary>
    private const string RuntimeProvidedHelperSource = """
        using eQuantic.UI.Primitives;

        namespace eQuantic.UI.Components;

        [RuntimeProvided]
        public static class PretendHelper
        {
            public const float Answer = 42;

            public static float Twice(float value) => value * 2;
        }
        """;

    [Fact]
    public void RuntimeProvidedStaticHelper_IsNotEmittedAsSharedModule()
    {
        var path = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Components", "PretendHelper.cs");

        var fenced = new ComponentCompiler { SymbolsAreAuthoritative = false }
            .CompileSource(RuntimeProvidedHelperSource, path)
            .ToList();
        fenced.Should().NotContain(result => result.ComponentName == "PretendHelper",
            "[RuntimeProvided] static helpers are supplied by @equantic/runtime, not emitted per app");

        // The A/B, and it is what makes the line above mean anything: the SAME source without the
        // attribute IS emitted. Shape kept from the contributor's original (#148).
        var emitted = new ComponentCompiler { SymbolsAreAuthoritative = false }
            .CompileSource(RuntimeProvidedHelperSource.Replace("[RuntimeProvided]", "", StringComparison.Ordinal), path)
            .ToList();

        emitted.Should().ContainSingle(result => result.ComponentName == "PretendHelper");
        emitted.Single().Success.Should().BeTrue(
            string.Join("; ", emitted.Single().Errors.Select(error => error.Message)));
    }

    /// <summary>
    /// AN EXTENSION GOES HOME, and an extension over the runtime VOCABULARY is no exception. JS has
    /// none, so <c>node.Centered()</c> lowers to <c>VisualNodeExtensions.centered(node)</c> with the
    /// receiver first — the shape every other reduced call already took.
    /// <para>
    /// It used to survive as a reduced call, on the reasoning that the runtime carries the
    /// behaviour as an instance method. It did, because the runtime mirrored it there for this
    /// lowering — on <c>VisualNode</c> AND on <c>Component</c>, with a <c>setCenterWrapper</c> seam
    /// between them to break the import cycle that arrangement created. Three artefacts, and the
    /// collision in #245: <c>class StatTile(string label, bool centered = false)</c> lowered its
    /// captured parameter to a field of that name, the field shadowed the method, and the page
    /// failed only in the browser — <c>statTile(...).centered is not a function</c>.
    /// </para>
    /// <para>
    /// Asked HERE because this is the pipeline where symbols bind. The same question asked of
    /// <c>ComponentCompiler.CompileSource</c> with no project compilation answers
    /// <c>.centered()</c> under either lowering — measured, which is why the coverage test that
    /// used to ask it there is gone rather than inverted.
    /// </para>
    /// </summary>
    [Fact]
    public void AnExtensionOverTheRuntimeVocabulary_GoesHomeToItsStatic()
    {
        var modules = TranspileSharedComponents();

        var home = modules.Where(module => module.Value.Contains("VisualNodeExtensions.centered(",
            StringComparison.Ordinal)).Select(module => module.Key).ToList();
        home.Should().NotBeEmpty("the shared library centres nodes in a dozen places");

        var reduced = modules
            .Where(module => Regex.IsMatch(module.Value, @"(?<!VisualNodeExtensions)\.centered\("))
            .Select(module => module.Key)
            .ToList();
        reduced.Should().BeEmpty(
            "an instance `centered()` is a member every component carries, and a component's own "
            + "primary-constructor parameter named `centered` shadows it (#245)");
    }

    /// <summary>
    /// The source this asks about: a STATIC HELPER that centres a node, and a component that calls
    /// it. Neither is library surface — the question is about the emission, and a production class
    /// carrying it would be a class kept alive for a test.
    /// </summary>
    private const string HelperCentresSource = """
        using eQuantic.UI.Primitives;

        namespace App;

        public static class Helpers
        {
            public static VisualNode Boxed() => new Text("x").Centered();
        }
        """;

    /// <summary>
    /// A HOME THE CONVERSION INTRODUCED IS IMPORTED WHEREVER IT IS INTRODUCED. The call is written
    /// on the receiver, so the home's name appears in no syntax the import scanner walks — the
    /// component path has always merged the conversion's own set for that reason, and the static
    /// helper path merged only the app-level half. Measured, before the fix:
    /// <code>
    /// import { Text } from "@equantic/runtime";
    /// export class Helpers {
    ///     static boxed() { return VisualNodeExtensions.centered(new Text('x')); }
    /// }
    /// </code>
    /// A qualified call to a name the module never imports — "VisualNodeExtensions is not defined",
    /// at LOAD rather than at the call, so the page shows nothing at all.
    /// <para>Mutation: drop the `UsedRuntimeTypes` union from the helper path and the import goes
    /// with it.</para>
    /// </summary>
    [Fact]
    public void AStaticHelper_ImportsTheExtensionHomeItCalls()
    {
        var path = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "Fixtures", "Helpers.cs");
        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(BindingCompilation(HelperCentresSource, path));

        var helper = compiler.CompileSource(HelperCentresSource, path)
            .Single(result => result.ComponentName == "Helpers").TypeScript;

        helper.Should().Contain("VisualNodeExtensions.centered(", "the extension goes home");
        helper.Should().MatchRegex(@"import \{[^}]*\bVisualNodeExtensions\b[^}]*\} from ""@equantic/runtime""",
            "and a qualified call to a name the module does not import fails the whole module at load");
    }

    /// <summary>
    /// The same question one module kind over: a RECORD whose method centres a node. A record is
    /// emitted by its own emitter with its own import block, so "the component path merges it" says
    /// nothing about this one.
    /// </summary>
    private const string RecordCentresSource = """
        using eQuantic.UI.Primitives;

        namespace App;

        public record Card(string Title)
        {
            public VisualNode Boxed() => new Text(Title).Centered();
        }
        """;

    /// <summary>
    /// AND THE RECORD PATH IS ITS OWN IMPORT BLOCK. `RecordTypeEmitter.EmitModule` builds its
    /// imports from a SYNTAX scan, and a reduced extension call names its home in no syntax — so
    /// the third module kind repeated the second one's bug. Measured, before the fix:
    /// <code>
    /// import { $eq, Text } from "@equantic/runtime";
    /// export class Card { … boxed() { return VisualNodeExtensions.centered(new Text(this.title)); } }
    /// </code>
    /// A qualified call to a name the module never imports, which fails at LOAD rather than at the
    /// call — so a page holding one such record renders nothing at all.
    /// <para>Mutation: drop the `UsedRuntimeTypes` union from the record path and the import goes
    /// with it, while the call stays.</para>
    /// </summary>
    [Fact]
    public void ARecord_ImportsTheExtensionHomeItCalls()
    {
        var path = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "Fixtures", "Card.cs");
        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(BindingCompilation(RecordCentresSource, path));

        var record = compiler.CompileSource(RecordCentresSource, path)
            .Single(result => result.ComponentName == "Card").TypeScript;

        record.Should().Contain("VisualNodeExtensions.centered(", "the extension goes home here too");
        record.Should().MatchRegex(@"import \{[^}]*\bVisualNodeExtensions\b[^}]*\} from ""@equantic/runtime""",
            "and a record module that names a home it does not import dies at load, like any other");
    }

    /// <summary>
    /// A Primitives extension whose home the runtime does NOT provide. `CurveEvaluator` is the
    /// cubic-bezier solver behind `Curve`; on the web a transition IS a CSS timing function, so the
    /// browser evaluates the curve and the runtime exports no twin.
    /// </summary>
    private const string CurveSource = """
        using eQuantic.UI.Primitives;

        namespace App;

        public static class Curves
        {
            public static float At(Curve curve, float t) => curve.Ease(t);
        }
        """;

    /// <summary>
    /// A HOME GOES HOME ONLY IF THE RUNTIME SAYS IT PROVIDES ONE. The namespace cannot decide it:
    /// `eQuantic.UI.Primitives` routes to the runtime implicitly, and it also holds types the
    /// runtime deliberately does not export. Sending `CurveEvaluator.Ease` home would emit
    /// `import { CurveEvaluator } from "@equantic/runtime"` against a bundle with no such export —
    /// which fails the whole module at LOAD, where the reduced form it had before fails only at the
    /// call. Neither works; one is strictly worse, and this PR must not introduce it.
    /// <para>
    /// `[RuntimeProvided]` is what decides, and it is the attribute's own contract ("the TS export
    /// must carry the SAME name"). `VisualNodeExtensions` carries it; `CurveEvaluator` does not.
    /// </para>
    /// <para>Mutation: take the attribute off `VisualNodeExtensions` and the shared twins revert to
    /// `.centered()`, failing the byte fixtures and the pin above; put one on `CurveEvaluator` and
    /// this case fails instead.</para>
    /// </summary>
    [Fact]
    public void AHomeTheRuntimeDoesNotProvide_KeepsTheReducedCall()
    {
        var path = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "Fixtures", "Curves.cs");
        var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
        compiler.SetProjectCompilation(BindingCompilation(CurveSource, path));

        var emitted = compiler.CompileSource(CurveSource, path)
            .Single(result => result.ComponentName == "Curves").TypeScript;

        emitted.Should().NotContain("CurveEvaluator",
            "the runtime exports no twin, so naming the home would import what the bundle has not");
        emitted.Should().Contain(".ease(", "it keeps the reduced form it always had");
    }

    /// <summary>The semantic setup the SDK gives eqc, for a source of this test's own.</summary>
    private static CSharpCompilation BindingCompilation(string source, string path)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(dll => dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(dll => (MetadataReference)MetadataReference.CreateFromFile(dll))
            .ToList();
        return CSharpCompilation.Create("BoundProbe",
            [
                CSharpSyntaxTree.ParseText(source, path: path),
                CSharpSyntaxTree.ParseText(
                    "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
                    path: "GlobalUsings.g.cs"),
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void SharedComponents_TranspiledFixtures_MatchCommittedModules()
    {
        var modules = TranspileSharedComponents();

        // ONE committed copy per module, and which one it is follows from what the module IS.
        //
        // - components/<N>.ts — every LIBRARY component, embedded in the runtime and re-exported
        //   from its index, with the import source rewritten to the internal aggregator. This set
        //   has to be on disk whatever else is true: the bundle imports it.
        // - __fixtures__/<N>.ts — the three app-shaped FIXTURES, keeping "@equantic/runtime",
        //   because what they exist to prove IS the per-app emission. Names carry no suffix so the
        //   relative imports between them resolve.
        //
        // There used to be a second copy of every library component under __transpiled__/, keeping
        // the package specifier for the specs to execute. It added no assertion: `Embedded` is a
        // pure rewrite, so one copy determines the other, and vitest ALIASES "@equantic/runtime" to
        // src/index.ts — the very distinction the second copy carried is erased by the config that
        // makes it runnable, and importing through the alias pulled the barrel (and the other copy)
        // back in as a cycle. The specs import components/ directly now, and ~8,000 generated lines
        // left the repository.
        var fixtureDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "__fixtures__");
        var embeddedDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "components");
        string Embedded(string typeScript) =>
            typeScript.Replace("from \"@equantic/runtime\"", "from \"../runtime-exports\"");
        // EVERY module the shared library produces is runtime-provided — a hardcoded roster here
        // meant a new component transpiled fine, never reached runtime.js, and only failed in the
        // browser with "does not provide an export named X". What the library declares IS the set.
        var embeddedNames = modules.Keys.Except(Fixtures).ToHashSet();

        // The BARREL: what the library exports is what the library CONTAINS. Hand-keeping the
        // roster in runtime-exports.ts had already fallen thirteen components behind — Table,
        // Select, Toast and friends transpiled, embedded, and could not be imported.
        var barrel = "// GENERATED by SharedComponentTranspilationTests — the shared library IS its\n"
            + "// directory, so this file is written from it rather than kept by hand.\n"
            + string.Concat(embeddedNames.OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"export {{ {name} }} from './{name}';\n"));
        var barrelPath = Path.Combine(embeddedDir, "index.ts");

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_TRANSPILED") == "1")
        {
            Directory.CreateDirectory(fixtureDir);
            Directory.CreateDirectory(embeddedDir);
            foreach (var (name, typeScript) in modules)
            {
                if (embeddedNames.Contains(name))
                    File.WriteAllText(Path.Combine(embeddedDir, $"{name}.ts"), Embedded(typeScript));
                else
                    File.WriteAllText(Path.Combine(fixtureDir, $"{name}.ts"), typeScript);
            }
            File.WriteAllText(barrelPath, barrel);
            return;
        }

        File.ReadAllText(barrelPath).Should().Be(barrel,
            "every component the library declares must be importable from the runtime — regenerate "
            + "with EQ_UPDATE_TRANSPILED=1");

        foreach (var (name, typeScript) in modules)
        {
            var isLibrary = embeddedNames.Contains(name);
            var path = Path.Combine(isLibrary ? embeddedDir : fixtureDir, $"{name}.ts");
            var expected = isLibrary ? Embedded(typeScript) : typeScript;

            File.Exists(path).Should().BeTrue(
                $"the runtime executes the transpiled {name} in vitest — generate once with EQ_UPDATE_TRANSPILED=1");
            File.ReadAllText(path).Should().Be(expected,
                $"the committed transpiled {name} must be regenerated (EQ_UPDATE_TRANSPILED=1) after compiler/component changes");
        }

        // Nothing but the barrel and the modules: a file left behind by a component that was renamed
        // or removed would go on being executed by a spec that still imports it, and the comparison
        // above cannot see a file it never looks for.
        var stray = Directory.GetFiles(embeddedDir, "*.ts")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != "index" && !embeddedNames.Contains(name!))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string.Join(", ", stray!).Should().BeEmpty(
            "these modules are in shared/components and the library no longer declares them — delete "
            + "them, or the runtime keeps embedding a component nothing produces");
    }

    [Fact]
    public void TranspiledButton_ImportsTheVocabularyFromTheRuntime()
    {
        var button = TranspileSharedComponents()["Button"];

        // Vocabulary types resolve to the runtime package (semantic namespace discovery), never ./modules.
        button.Should().Contain("from \"@equantic/runtime\"");
        button.Should().NotContain("from \"./Box\"");
        button.Should().NotContain("from \"./Variant\"", "enums lower to string literals and are never imported");

        // The C# defaults survive as JS parameter defaults.
        button.Should().Contain("variant: any = 'primary'");
        button.Should().Contain("size: any = 'medium'");

        // The BoxStyle object initializer survives as a config object (was silently dropped before).
        button.Should().Contain("new BoxStyle({ height: height");
        // A `const` INLINES at the use site — the same thing Roslyn does for a C# consumer, and what
        // lets a component default to a constant from an assembly the bundle never ships.
        button.Should().Contain("minWidth: 64");

        // Each rung read on its own, the way every other component reads the ladder. This asserted a
        // seven-slot array deconstruction while `ButtonStyles.metrics` existed to hand the twin a
        // tuple; the numbers were always these calls, and now the twin makes them.
        button.Should().Contain("let height = Sizing.height(this.size, context.density)");
        button.Should().Contain("let labelSize = Sizing.labelSize(this.size, context.density)");
        button.Should().Contain("let gap = Sizing.gap(this.size)");
        button.Should().NotContain("ButtonStyles", "the tuple view is gone, and the ladder is the surface");
        // Shape is theme-driven (Material overrides the ladder) — resolved from the enum member string.
        button.Should().Contain("theme.shape(this.size === 'xLarge' ? 'large' : 'medium')");
    }

    /// <summary>A CORE page composing shared components through the adapter — the unification bridge.</summary>
    private const string BridgePageSource = """
        using eQuantic.UI.Primitives;
        using eQuantic.UI.Components;
        using eQuantic.UI.Web;

        namespace eQuantic.UI.Web.Tests.Fixtures;

        public class SharedBridgePage : StatelessComponent
        {
            public override IComponent Build(RenderContext context) =>
                new VisualNodeComponent(new Card(new Button("Ok"), CardKind.Outlined));
        }
        """;

    [Fact]
    public void CorePage_ComposingSharedThroughTheAdapter_ImportsItFromTheRuntime()
    {
        var root = RepoRoot();
        var pagePath = Path.Combine(root, "tests", "eQuantic.UI.Web.Tests", "Fixtures", "SharedBridgePage.cs");
        var trees = new List<Microsoft.CodeAnalysis.SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(BridgePageSource, path: pagePath),
            CSharpSyntaxTree.ParseText(
                "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
                path: "GlobalUsings.g.cs"),
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create("BridgePage", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(BridgePageSource, pagePath).Single();

        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        // The adapter carries [RuntimeProvided] → routed to the runtime package, not ./VisualNodeComponent.
        result.TypeScript.Should().Contain("VisualNodeComponent");
        result.TypeScript.Should().Contain("from \"@equantic/runtime\"");
        result.TypeScript.Should().NotContain("from \"./VisualNodeComponent\"");
        result.TypeScript.Should().Contain("new VisualNodeComponent(new Card(new Button('Ok'), 'outlined'))");
    }

    /// <summary>Compiles a single page source against the full reference set (helper for routing tests).</summary>
    private static string TranspilePage(string source, string fileName)
    {
        var pagePath = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "Fixtures", fileName);
        var trees = new List<Microsoft.CodeAnalysis.SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(source, path: pagePath),
            CSharpSyntaxTree.ParseText(
                "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
                path: "GlobalUsings.g.cs"),
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create("PageProbe", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, pagePath).Single();
        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void SharedLibraryTypes_AreRuntimeProvided_NeverPerAppModules()
    {
        // The shared-library Button (the ONLY Button since the legacy web set was excised) is
        // runtime-provided: the import routes to @equantic/runtime, never to a per-app module.
        var sharedPage = TranspilePage("""
            using eQuantic.UI.Primitives;
            using eQuantic.UI.Components;
            using eQuantic.UI.Web;

            namespace eQuantic.UI.Web.Tests.Fixtures;

            public class SharedButtonPage : StatelessComponent
            {
                public override IComponent Build(RenderContext context) =>
                    new VisualNodeComponent(new Button("shared"));
            }
            """, "SharedButtonPage.cs");

        sharedPage.Should().Contain("Button");
        sharedPage.Should().Contain("from \"@equantic/runtime\"");
        sharedPage.Should().NotContain("from \"./Button\"");
    }

    [Fact]
    public void TranspiledSharedCounter_UsesTheDirectStatefulShape()
    {
        var counter = TranspileSharedComponents()["SharedCounter"];

        // The stateful shape: direct SetState on the component.
        counter.Should().Contain("extends StatefulComponent");
        counter.Should().NotContain("createState");

        // C# implicit field default: `private int _count;` must initialize (undefined would poison ++).
        counter.Should().Contain("_count: number = 0;");

        // The NAMED argument lands at the constructor's real position, with the skipped parameters
        // filled from their C# defaults — JS has no named arguments.
        counter.Should().Contain("new Button('Increment', 'primary', 'medium', () => this.setState(() => this._count++))");

        // async void — the modifier drives the emission, not the return type (the site's
        // hover-intent grace timer found this: awaits inside a non-async function don't parse).
        counter.Should().Contain("async bumpSoon(");

        // Iterator methods MATERIALISE: every sequence in the emitted world is an array, so an
        // iterator fills one and returns it. As a JS generator it looked right and then read as
        // undefined the moment any LINQ operator touched the result.
        counter.Should().Contain("cells() {").And.NotContain("*cells(");
        counter.Should().Contain("const _seq = []");
        counter.Should().Contain("_seq.push(new Text('a', 'caption'))");
        counter.Should().Contain("if (this._count > 3) return _seq;", "`yield break` returns what it built");
    }

    [Fact]
    public void ATranspiledConstructor_AppliesTheConfigObjectLAST()
    {
        var button = TranspileSharedComponents()["Button"];

        // A C# object initializer runs AFTER the constructor, so the config object must be applied
        // after every positional assignment and after the C# body. Handing it to `super()` first put
        // it first, and each positional parameter's own default then overwrote it — which is how
        // `new Button(label, Variant.Outline, SizeVariant.Small) { OnPressed = f }` shipped with
        // `onPressed = null`: a button that renders perfectly and answers nothing.
        button.Should().Contain("super();").And.NotContain("super(props);");
        button.Should().Contain("if (props && typeof props === 'object') Object.assign(this, props);");

        var assign = button.IndexOf("Object.assign(this, props)", StringComparison.Ordinal);
        var positional = button.IndexOf("if (onPressed !== undefined) this.onPressed = onPressed;",
            StringComparison.Ordinal);
        assign.Should().BeGreaterThan(positional, "the initializer is what the author wrote last");
    }

    [Fact]
    public void TranspiledNestedChild_CarriesTheAdoptConfigContract()
    {
        var modules = TranspileSharedComponents();
        var child = modules["NestedChild"];

        // The reconciler contract transpiles as-is: adoptConfig with the declaration-pattern type
        // check, copying the fresh configuration into the retained instance.
        child.Should().Contain("adoptConfig(");
        child.Should().Contain("instanceof NestedChild");
        child.Should().Contain("this._label = ");

        // The host composes the child positionally; the web store reconciles it by lowering path.
        modules["NestedHost"].Should().Contain("new NestedChild(");
    }

    [Fact]
    public void SharedComponentsNeverRebuildAPositionalTwinFromAnObject()
    {
        // A positional twin takes its arguments IN ORDER, so `new TypeStyle({...})` reaches the
        // browser as a control with no size at all. The compiler's rule (patch, never rebuild) is
        // pinned in eQuantic.UI.Compiler.Tests — PositionalRecordWithTests — where it belongs; what
        // this guards is that no shared component ever emits the broken shape.
        foreach (var (name, ts) in TranspileSharedComponents())
            ts.Should().NotContain("new TypeStyle({", $"{name} rebuilds a positional twin");
    }

}
