using eQuantic.UI.Compiler;
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
}
