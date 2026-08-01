using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Transpiles the REAL shared component sources (<c>eQuantic.UI.Components</c>) with the real
/// compiler — the same pipeline an app build runs — and pins the emitted modules committed at
/// <c>src/eQuantic.UI.Runtime/src/shared/__transpiled__/</c>, where the runtime's vitest suite
/// EXECUTES them against the vocabulary classes and the generated theme (the write-once proof on
/// web: C# source → eqc → JS → the same DOM the C# WebRealizer produces). Refresh the fixtures with
/// <c>EQ_UPDATE_TRANSPILED=1</c> after compiler or component changes.
/// </summary>
public class SharedComponentTranspilationTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static readonly string[] SharedSources =
    [
        "Button.cs", "Card.cs", "Divider.cs", "Badge.cs", "Chip.cs", "ProgressBar.cs", "Avatar.cs",
        "Banner.cs", "IconButton.cs", "Checkbox.cs", "Switch.cs", "RadioGroup.cs", "ListItem.cs",
        "Tabs.cs", "EmptyState.cs", "AppBar.cs", "BottomNavigation.cs", "TextInput.cs", "SearchField.cs", "Dialog.cs",
        "Toast.cs", "BottomSheet.cs",
        "Menu.cs", "Select.cs", "Popover.cs",
        "Tooltip.cs", "Drawer.cs", "Accordion.cs", "Table.cs",
    ];

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
        var sourcePaths = SharedSources
            .Select(name => Path.Combine(root, "src", "eQuantic.UI.Components", name))
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

        var compiler = new ComponentCompiler();
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

    [Fact]
    public void SharedComponents_TranspiledFixtures_MatchCommittedModules()
    {
        var modules = TranspileSharedComponents();
        var fixtureDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "__transpiled__");

        // Two generated copies of the same pin:
        // - __transpiled__/<N>.ts — the exact per-app emission (imports "@equantic/runtime"), executed
        //   by the parity specs. Names carry NO suffix so relative imports between fixtures resolve.
        // - components/<N>.ts (library components only) — the SAME bytes with the import source
        //   rewritten to the internal aggregator, EMBEDDED in the runtime and re-exported from its
        //   index (the shared library is runtime-provided; apps import { Button } from the runtime).
        var embeddedDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "components");
        string Embedded(string typeScript) =>
            typeScript.Replace("from \"@equantic/runtime\"", "from \"../runtime-exports\"");
        var embeddedNames = new[]
        {
            "Button", "Card", "Divider", "Badge", "Chip", "ProgressBar", "Avatar", "Banner",
            "IconButton", "Checkbox", "Switch", "RadioGroup", "ListItem", "List", "Tabs",
            "EmptyState", "Skeleton", "AppBar", "BottomNavigation", "NavItem", "TextInput", "SearchField", "Dialog", "DialogAction",
            "Toast", "BottomSheet",
            "Menu", "MenuItem", "Select", "Popover",
            "Tooltip", "Drawer", "Accordion", "AccordionItem", "Table",
        };

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_TRANSPILED") == "1")
        {
            Directory.CreateDirectory(fixtureDir);
            Directory.CreateDirectory(embeddedDir);
            foreach (var (name, typeScript) in modules)
            {
                File.WriteAllText(Path.Combine(fixtureDir, $"{name}.ts"), typeScript);
                if (embeddedNames.Contains(name))
                    File.WriteAllText(Path.Combine(embeddedDir, $"{name}.ts"), Embedded(typeScript));
            }
            return;
        }

        foreach (var (name, typeScript) in modules)
        {
            var path = Path.Combine(fixtureDir, $"{name}.ts");
            File.Exists(path).Should().BeTrue(
                $"the runtime executes the transpiled {name} in vitest — generate once with EQ_UPDATE_TRANSPILED=1");
            File.ReadAllText(path).Should().Be(typeScript,
                $"the committed transpiled {name} must be regenerated (EQ_UPDATE_TRANSPILED=1) after compiler/component changes");

            if (embeddedNames.Contains(name))
            {
                File.ReadAllText(Path.Combine(embeddedDir, $"{name}.ts")).Should().Be(Embedded(typeScript),
                    $"the runtime-embedded {name} must match the pinned emission (EQ_UPDATE_TRANSPILED=1 regenerates both)");
            }
        }
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

        // The size-table tuple deconstructs as an array — the generated ButtonStyles.metrics shape.
        button.Should().Contain("let [height, padX, gap, labelSize, iconSize, , ] = ButtonStyles.metrics(this.size)");
        // Shape is theme-driven (Material overrides the ladder) — resolved from the enum member string.
        button.Should().Contain("theme.shape(this.size === 'xLarge' ? 'large' : 'medium')");
    }

    /// <summary>A CORE page composing shared components through the adapter — the unification bridge.</summary>
    private const string BridgePageSource = """
        using eQuantic.UI.Core;
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
            using eQuantic.UI.Core;
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

        // The Primitives stateful shape: direct SetState on the component, no CreateState split.
        counter.Should().Contain("extends SharedStatefulComponent");
        counter.Should().NotContain("createState");

        // C# implicit field default: `private int _count;` must initialize (undefined would poison ++).
        counter.Should().Contain("_count: number = 0;");

        // The NAMED argument lands at the constructor's real position, with the skipped parameters
        // filled from their C# defaults — JS has no named arguments.
        counter.Should().Contain("new Button('Increment', 'primary', 'medium', () => this.setState(() => this._count++))");

        // async void — the modifier drives the emission, not the return type (the site's
        // hover-intent grace timer found this: awaits inside a non-async function don't parse).
        counter.Should().Contain("async bumpSoon(");

        // Iterator methods are generators: `*cells()` (yield parses only inside one).
        counter.Should().Contain("*cells(");
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
}
