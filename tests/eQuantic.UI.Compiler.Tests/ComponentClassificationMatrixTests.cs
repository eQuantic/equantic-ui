using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// WHAT A COMPONENT CARRIES INTO ITS TWIN MUST NOT DEPEND ON THE NAME OF THE BASE IT WAS WRITTEN
/// OVER. This is the matrix #272 asks for: base shapes crossed with member kinds, every cell
/// asserting that the member reaches the emission AND that the module it lands in can resolve what
/// the member's body names.
///
/// <para>
/// The defect this pins is one shape seen three times. <c>ComponentParser</c> answers two questions
/// about the same class by different means: detection walks the base chain through the semantic
/// model, and classification then compares <c>BaseList.Types.First().ToString()</c> against three
/// literals. Each of the four arms keeps its own hand-written list of which sub-parsers to call and
/// all four disagree — so a component over an app-owned base loses its members (#268), a
/// <c>[ServerAction]</c> on a stateless component emits no stub (#269), and #245 was the same
/// shape reached by a third route. None of the three is a compile error, a diagnostic or a test
/// failure: it is a page that builds clean and dies in the browser.
/// </para>
///
/// <para>
/// THE IMPORT HALF IS NOT DECORATION. Measured on the published 0.2.0-preview.56, a component over
/// an app-owned base keeps the CALL, loses the DEFINITION and loses the IMPORT — so a pin that
/// checked only the member would pass on a module that cannot load. Every cell asserts both.
/// </para>
///
/// <para>
/// A MATRIX rather than the two cells the issues happen to name, because the point is the shape.
/// The cells that already worked are in it on purpose: they are what says the collapse changed
/// nothing for them.
/// </para>
/// </summary>
public class ComponentClassificationMatrixTests
{
    /// <summary>The base chain a probe is written over: what precedes it, what it writes, and
    /// whether the runtime base it ultimately reaches is the stateful one.</summary>
    private sealed record BaseShape(string Preamble, string Written, bool Stateful);

    /// <summary>A member kind: what the probe declares, what <c>Build</c> does with it, and the
    /// text the twin must carry if the member survived.</summary>
    private sealed record MemberKind(string Declaration, string BuildBody, string MustCarry);

    private static readonly Dictionary<string, BaseShape> Bases = new()
    {
        ["direct-stateless"] = new("", "StatelessComponent", false),
        ["direct-stateful"] = new("", "StatefulComponent", true),
        ["app-base"] = new(
            "public abstract class OneBase : StatelessComponent { }",
            "OneBase", false),
        ["app-base-twice"] = new(
            """
            public abstract class OuterBase : StatelessComponent { }
            public abstract class InnerBase : OuterBase { }
            """,
            "InnerBase", false),
        ["app-base-over-stateful"] = new(
            "public abstract class StatefulBase : StatefulComponent { }",
            "StatefulBase", true),
    };

    private static readonly Dictionary<string, MemberKind> Members = new()
    {
        ["constructor"] = new(
            """
            private readonly string _seed;
            public Probe(string seed) { _seed = seed; }
            """,
            "new Text(_seed)", "constructor("),
        ["field"] = new(
            "private string _carried = \"c\";",
            "new Text(_carried)", "_carried"),
        ["property"] = new(
            "public string Caption { get; set; } = \"c\";",
            "new Text(Caption)", "caption"),
        ["instance-method"] = new(
            "private string Label() => \"l\";",
            "new Text(Label())", "label("),
        ["static-method"] = new(
            "private static VisualNode Tile(string s) => new Text(s);",
            "Tile(\"x\")", "tile("),
        ["server-action"] = new(
            """
            [ServerAction]
            public async Task<string> Load() { await Task.Delay(1); return "x"; }
            """,
            "new Text(\"t\")", "getServerActionsClient().invoke('Probe/Load'"),
    };

    private static string Source(string baseId, string memberId)
    {
        var shape = Bases[baseId];
        var member = Members[memberId];
        return $$"""
            using eQuantic.UI.Primitives;

            {{shape.Preamble}}

            public sealed class Probe : {{shape.Written}}
            {
                {{member.Declaration}}

                public override VisualNode Build(ComponentContext context) => {{member.BuildBody}};
            }
            """;
    }

    private static CompilationResult Compile(string baseId, string memberId)
    {
        var source = Source(baseId, memberId);
        var results = new ComponentCompiler().CompileSource(source, "Probe.cs");
        var probe = results.FirstOrDefault(r => r.ComponentName == "Probe");
        Assert.True(probe is not null,
            $"no module was emitted for Probe at [{baseId} x {memberId}]. Emitted: " +
            string.Join(", ", results.Select(r => r.ComponentName)));
        return probe!;
    }

    [Theory]
    [InlineData("direct-stateless", "constructor")]
    [InlineData("direct-stateless", "field")]
    [InlineData("direct-stateless", "property")]
    [InlineData("direct-stateless", "instance-method")]
    [InlineData("direct-stateless", "static-method")]
    [InlineData("direct-stateless", "server-action")]
    [InlineData("direct-stateful", "constructor")]
    [InlineData("direct-stateful", "field")]
    [InlineData("direct-stateful", "property")]
    [InlineData("direct-stateful", "instance-method")]
    [InlineData("direct-stateful", "static-method")]
    [InlineData("direct-stateful", "server-action")]
    [InlineData("app-base", "constructor")]
    [InlineData("app-base", "field")]
    [InlineData("app-base", "property")]
    [InlineData("app-base", "instance-method")]
    [InlineData("app-base", "static-method")]
    [InlineData("app-base", "server-action")]
    [InlineData("app-base-twice", "constructor")]
    [InlineData("app-base-twice", "field")]
    [InlineData("app-base-twice", "property")]
    [InlineData("app-base-twice", "instance-method")]
    [InlineData("app-base-twice", "static-method")]
    [InlineData("app-base-twice", "server-action")]
    [InlineData("app-base-over-stateful", "constructor")]
    [InlineData("app-base-over-stateful", "field")]
    [InlineData("app-base-over-stateful", "property")]
    [InlineData("app-base-over-stateful", "instance-method")]
    [InlineData("app-base-over-stateful", "static-method")]
    [InlineData("app-base-over-stateful", "server-action")]
    public void AMemberReachesTheTwin_WhateverBaseItWasWrittenOver(string baseId, string memberId)
    {
        var result = Compile(baseId, memberId);
        var must = Members[memberId].MustCarry;

        Assert.True(result.Success,
            $"[{baseId} x {memberId}] did not compile: " +
            string.Join("; ", result.Errors.Select(e => e.Message)));

        // AGAINST THE TWIN WITHOUT build(), and that is the whole point of this guard. Every
        // member is NAMED inside build() whether or not it was emitted — `this.label()`,
        // `this._carried`, `Probe.tile(...)` are written by the body's own translation — so a
        // Contains over the whole module is satisfied by the CALL and passes on the exact defect
        // #268 reports. Measured: over an app-owned base the twin is build() and nothing else,
        // and every one of these markers is present in it.
        var declarations = WithoutBuild(result.TypeScript);
        Assert.True(declarations.Contains(must, StringComparison.Ordinal),
            $"[{baseId} x {memberId}] lost its member — nothing outside build() declares `{must}`:\n\n{result.TypeScript}");
    }

    /// <summary>
    /// The twin with <c>build()</c>'s body removed, so a marker found in what remains is a
    /// DECLARATION rather than a reference the body made to something that does not exist.
    /// </summary>
    private static string WithoutBuild(string twin)
    {
        var lines = twin.Split('\n');
        var kept = new List<string>();
        var inBuild = false;
        foreach (var line in lines)
        {
            if (!inBuild && line.StartsWith("    build(", StringComparison.Ordinal))
            {
                inBuild = true;
                continue;
            }
            if (inBuild)
            {
                if (line.StartsWith("    }", StringComparison.Ordinal)) inBuild = false;
                continue;
            }
            kept.Add(line);
        }
        return string.Join("\n", kept);
    }

    /// <summary>
    /// The general net behind the per-cell markers: whatever <c>build()</c> reaches for on its own
    /// class must exist on the module. This is the browser's own reading — `this.label is not a
    /// function` is what a page dies of — and it catches a member kind this matrix does not
    /// enumerate.
    /// </summary>
    [Theory]
    [InlineData("direct-stateless")]
    [InlineData("direct-stateful")]
    [InlineData("app-base")]
    [InlineData("app-base-twice")]
    [InlineData("app-base-over-stateful")]
    public void BuildNeverReachesForAMemberTheModuleDoesNotDefine(string baseId)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;

            {{Bases[baseId].Preamble}}

            public sealed class Probe : {{Bases[baseId].Written}}
            {
                private string _carried = "c";
                private string Label() => "l";
                private static VisualNode Tile(string s) => new Text(s);

                public override VisualNode Build(ComponentContext context) => Tile(_carried + Label());
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "Probe.cs")
            .First(r => r.ComponentName == "Probe").TypeScript;

        var declarations = WithoutBuild(twin);
        var reached = System.Text.RegularExpressions.Regex
            .Matches(twin, @"(?:this|Probe)\.(\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(reached);
        var dangling = reached
            .Where(name => !declarations.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.True(dangling.Count == 0,
            $"[{baseId}] build() reaches for {string.Join(", ", dangling)} and the module defines " +
            $"none of them — the page dies at first render:\n\n{twin}");
    }

    /// <summary>
    /// The import half, and it needs the type named in a member OTHER than <c>build</c> — which is
    /// what makes it a second test rather than a clause of the first.
    ///
    /// <para>
    /// MEASURED: with <c>new Text(...)</c> written directly in <c>build</c>, the import survives
    /// even on the broken shape, because the emitter reads the body it did emit. The defect only
    /// shows when the type is named by a member that was DROPPED: the helper goes, its import goes
    /// with it, and what is left is a module whose build calls a static that does not exist. A
    /// version of this test with <c>Text</c> in build passes on current main and would have
    /// pinned nothing.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("direct-stateless")]
    [InlineData("direct-stateful")]
    [InlineData("app-base")]
    [InlineData("app-base-twice")]
    [InlineData("app-base-over-stateful")]
    public void TheTwinImportsWhatADroppedMemberWouldHaveNamed(string baseId)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;

            {{Bases[baseId].Preamble}}

            public sealed class Probe : {{Bases[baseId].Written}}
            {
                private static VisualNode Tile(string s) => new Text(s);

                public override VisualNode Build(ComponentContext context) => Tile("x");
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "Probe.cs")
            .First(r => r.ComponentName == "Probe").TypeScript;

        var imports = string.Join(
            "\n",
            twin.Split('\n').Where(line => line.TrimStart().StartsWith("import", StringComparison.Ordinal)));

        Assert.True(imports.Contains("Text", StringComparison.Ordinal),
            $"[{baseId}] emits a member naming Text and imports no Text — the module cannot load.\n\n" +
            $"imports:\n{imports}\n\nfull twin:\n{twin}");
    }

    /// <summary>
    /// A component that declares NO Build over a base that has one: the twin must emit no
    /// <c>build</c> at all, so JavaScript's own prototype chain answers.
    ///
    /// <para>
    /// THIS IS A DEFECT THE COLLAPSE ITSELF CREATED, kept as a cell because it is the fourth
    /// instance of the same shape rather than a detail of the refactor. The emitter falls back to
    /// <c>throw new Error('Build method not implemented')</c> when a component has no Build — honest
    /// over an abstract framework base, and a REGRESSION over an app-owned one, where it overrides a
    /// working inherited build with a throw. The old fourth arm hid this by marking such a class
    /// primitive; collapsing the arms routed it down the component path and the stub appeared.
    /// </para>
    ///
    /// <para>
    /// The A/B: with <c>BuildComesFromTheBase</c> forced false, this cell emits the throwing build
    /// and fails here alone — the rest of the matrix declares its own Build and never reaches the
    /// fallback.
    /// </para>
    /// </summary>
    [Fact]
    public void AComponentThatInheritsItsBuild_OverridesItWithNothing()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            public abstract class OneBase : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text("base");
            }

            public sealed class Probe : OneBase
            {
                private string _carried = "c";
                private string Label() => _carried;
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "Probe.cs")
            .First(r => r.ComponentName == "Probe").TypeScript;

        Assert.False(twin.Contains("Build method not implemented", StringComparison.Ordinal),
            $"Probe inherits a working build from OneBase and the twin overrides it with a throw:\n\n{twin}");
        Assert.False(twin.Contains("build(", StringComparison.Ordinal),
            $"Probe declares no Build, so its twin must declare none either:\n\n{twin}");

        // …and it still carries its own members, which is the rest of this matrix's point.
        Assert.Contains("_carried", twin, StringComparison.Ordinal);
        Assert.Contains("label(", twin, StringComparison.Ordinal);
    }

    /// <summary>
    /// The base's own members, which is this failure one level up and the reason the consumer path
    /// still died after the arms were collapsed.
    ///
    /// <para>
    /// An app-owned base is normally written as <c>abstract class CardBase : StatelessComponent</c>
    /// holding shared helpers and NO Build — there is nothing for it to build. The emitter's
    /// condition for "has anything worth emitting" tested only for a Build, while the comment above
    /// it already claimed it tested for members too, so the helpers were dropped and a subclass
    /// calling one died on `this.frame is not a function`.
    /// </para>
    ///
    /// <para>
    /// PRE-EXISTING, and measured as such: the same probe emits the same empty base against main's
    /// compiler, so the collapse neither caused it nor exposed it. It is here because #272's own
    /// done-criteria include a component over an app-owned base that renders — and without this the
    /// child keeps its members and still calls into a base that has none.
    /// </para>
    /// </summary>
    [Fact]
    public void AnAbstractBaseKeepsTheHelpersItsSubclassesCall()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            public abstract class CardBase : StatelessComponent
            {
                protected string Frame(string inner) => "[" + inner + "]";
            }

            public sealed class Probe : CardBase
            {
                public override VisualNode Build(ComponentContext context) => new Text(Frame("x"));
            }
            """;
        var all = new ComponentCompiler().CompileSource(source, "Probe.cs");
        var baseTwin = all.First(r => r.ComponentName == "CardBase").TypeScript;
        var childTwin = all.First(r => r.ComponentName == "Probe").TypeScript;

        Assert.Contains("frame(", baseTwin, StringComparison.Ordinal);
        Assert.Contains("this.frame(", childTwin, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>[ServerAction]</c> on an ABSTRACT component is refused (EQ2012), and this is the one
    /// place where making the stateless path parse server actions had to be FENCED rather than
    /// simply enabled.
    ///
    /// <para>
    /// The two sides disagree about who owns an inherited action, and both are silent about it.
    /// <c>ServerActionRegistry.ScanAssembly</c> skips abstract types and walks inherited methods,
    /// registering each under the CONCRETE component's name, so the server serves <c>Tile/Load</c>.
    /// eqc emits a stub in the module that DECLARES the method, which is the base's, so the client
    /// invokes <c>CardBase/Load</c>. Measured end to end before the fence: the emitted base module
    /// carried `invoke('CardBase/Load', [])` and nothing serves that id.
    /// </para>
    ///
    /// <para>
    /// An alias would not settle it — the descriptor carries the component TYPE to invoke on, so two
    /// children inheriting one action give the base's id two answers. That is a decision about
    /// ownership, not a patch, so the shape is refused rather than half-supported.
    /// </para>
    /// </summary>
    [Fact]
    public void AServerActionOnAnAbstractComponent_IsRefused()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            public abstract class CardBase : StatelessComponent
            {
                protected string Frame(string s) => s;

                [ServerAction]
                public async Task<string> Load() { await Task.Delay(1); return "x"; }
            }

            public sealed class Tile : CardBase
            {
                public override VisualNode Build(ComponentContext context) => new Text(Frame("x"));
            }
            """;
        var all = new ComponentCompiler().CompileSource(source, "Tile.cs");
        var baseResult = all.First(r => r.ComponentName == "CardBase");

        Assert.False(baseResult.Success);
        Assert.Contains(baseResult.Errors, e => e.Code == "EQ2012");

        // The concrete component is untouched: #269 is a [ServerAction] on a CONCRETE stateless
        // component, and fencing the abstract case must not take that back.
        var concrete = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;

            public sealed class Solo : StatelessComponent
            {
                [ServerAction]
                public async Task<string> Load() { await Task.Delay(1); return "x"; }

                public override VisualNode Build(ComponentContext context) => new Text("t");
            }
            """, "Solo.cs").First(r => r.ComponentName == "Solo");

        Assert.True(concrete.Success);
        Assert.Contains("invoke('Solo/Load'", concrete.TypeScript, StringComparison.Ordinal);
    }

    /// <summary>
    /// The PRIMITIVE path, which the member matrix deliberately cannot cover: an
    /// <c>HtmlElement</c> transcribes a DOM tag, has no Build and no server actions, and parses
    /// through <c>ParsePrimitiveClass</c> instead of the member parsers. It is the one arm the
    /// collapse KEPT, so it needs the regression case the matrix's shapes cannot give it — without
    /// this, a break in the resolved-<c>HtmlElement</c> branch passes the whole guard.
    /// </summary>
    [Fact]
    public void AnHtmlElementStillTakesThePrimitivePath()
    {
        const string source = """
            using eQuantic.UI.Primitives;
            using eQuantic.UI.Web;

            public sealed class MyTag : HtmlElement
            {
                public string Href { get; set; } = "";
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "MyTag.cs")
            .First(r => r.ComponentName == "MyTag").TypeScript;

        Assert.Contains("extends HtmlElement", twin, StringComparison.Ordinal);
        Assert.Contains("render()", twin, StringComparison.Ordinal);
        Assert.Contains("href", twin, StringComparison.Ordinal);

        // The primitive path has no Build, so it must not acquire the component path's stub.
        Assert.DoesNotContain("Build method not implemented", twin, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>IsStateful</c> follows the RESOLVED base, asserted through the one thing it actually
    /// changes.
    ///
    /// <para>
    /// The matrix's member cells cannot see it: stateful and stateless emit identical bodies, and
    /// the <c>extends</c> clause beside them comes from <c>BaseClassName</c> — the WRITTEN name —
    /// not from <c>IsStateful</c>. So a guard that asserted the emitted runtime base would pin the
    /// written name and pass with <c>IsStateful</c> stuck at false, which is the trap this whole
    /// file keeps falling into.
    /// </para>
    ///
    /// <para>
    /// What it really gates is <c>SemanticValidator</c>: the client/server boundary is enforced for
    /// STATEFUL components only. Measured — a component over a base that ultimately extends
    /// <c>StatefulComponent</c> is refused EQ2112 for touching <c>System.IO</c>, and on main, where
    /// the fourth arm hardcoded <c>IsStateful = false</c>, the same component was waved through.
    /// </para>
    /// </summary>
    [Fact]
    public void TheClientBoundaryFollowsTheResolvedBase()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            namespace Demo;

            public abstract class StatefulBase : StatefulComponent { }

            public sealed class Probe : StatefulBase
            {
                private string Read() => System.IO.File.ReadAllText("x");
                public override VisualNode Build(ComponentContext context) => new Text(Read());
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("BoundaryProbe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").First(r => r.ComponentName == "Probe");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "EQ2112");
    }

    /// <summary>
    /// The abstract base's OTHER member kinds, which the helper cell above does not reach and which
    /// review caught: the emitter's "has anything worth emitting" test counted methods only, so a
    /// base carrying just a property or just a constructor was still emitted empty.
    ///
    /// <para>
    /// Each measured on its own. A PROPERTY costs more than it looks — an auto-property's default is
    /// applied in the generated constructor, so dropping it does not merely omit a declaration, it
    /// hands a child that supplies no value <c>undefined</c> where the C# says <c>"c"</c>. A
    /// CONSTRUCTOR takes its body and its parameters with it.
    /// </para>
    ///
    /// <para>
    /// A FIELD is deliberately not in here: measured, it was never lost — fields are emitted by the
    /// branch above this condition and survived even before any of this. Asserting one would have
    /// passed either way and read like coverage.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("property", "public string Caption { get; set; } = \"c\";", "caption")]
    [InlineData("computed-property", "protected string Label => \"l\";", "label")]
    [InlineData("constructor", "protected CardBase(string seed) { }", "constructor(")]
    public void AnAbstractBaseKeepsItsOtherMembersToo(string kind, string declaration, string must)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;

            public abstract class CardBase : StatelessComponent
            {
                {{declaration}}
            }

            public sealed class Tile : CardBase
            {
                public override VisualNode Build(ComponentContext context) => new Text("x");
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "Tile.cs")
            .First(r => r.ComponentName == "CardBase").TypeScript;

        Assert.True(twin.Contains(must, StringComparison.Ordinal),
            $"an abstract base carrying only a {kind} was emitted without it:\n\n{twin}");
    }
}
