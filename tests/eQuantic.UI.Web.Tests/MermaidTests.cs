using System.Runtime.CompilerServices;
using eQuantic.UI.Components;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The mermaid subset's truth suite — parser semantics, the fallback contract, and the C# half of
/// the LAYOUT parity pin. The layout runs on every side (C# for SSR and Photon, the transpiled
/// twin in the browser) and keeps to integers and exact halves precisely so both compilations
/// place every box on identical numbers; the pinned fixture at
/// <c>src/eQuantic.UI.Runtime/src/shared/__fixtures__/mermaid-layout.txt</c> is that promise made
/// executable. <c>mermaid.spec.ts</c> asserts the SAME file against the twin's geometry — one
/// dumper, mirrored line for line, one fixture. Regenerate with <c>EQ_UPDATE_MERMAID_FIXTURE=1</c>.
/// </summary>
public class MermaidTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string FixturePath => Path.Combine(RepoRoot(),
        "src", "eQuantic.UI.Runtime", "src", "shared", "__fixtures__", "mermaid-layout.txt");

    public static readonly string Flowchart = string.Join("\n",
    [
        "graph TD",
        "  A[Start] --> B{Valid?}",
        "  B -->|yes| C(Deploy)",
        "  B -->|no| D[Fix]",
        "  D --> A",
        "  C --> E((Done))",
    ]);

    public static readonly string Sequence = string.Join("\n",
    [
        "sequenceDiagram",
        "  participant U as User",
        "  participant S as Server",
        "  U->>S: POST /login",
        "  S-->>U: 200 + token",
        "  S->>S: audit",
    ]);

    // The wiki's own architecture diagram — the LR-with-labels case that forced the label-aware
    // rank gaps (a chip wider than the default gap slid under the next rank's nodes).
    public static readonly string FlowchartLr = string.Join("\n",
    [
        "graph LR",
        "  A[C# components] --> B{eqc}",
        "  B -->|static shell| C[SSR HTML]",
        "  B -->|dynamic logic| D(TypeScript)",
        "  C --> E{Embedded Bun}",
        "  D --> E",
        "  E --> F[wwwroot/_equantic]",
    ]);

    // ---- The shared dumper (mirrored in mermaid.spec.ts) ---------------------------------------

    /// <summary>Invariant float text — the fixture must read identically on any machine culture,
    /// and identically to what JavaScript's number-to-string produces for the same value.</summary>
    private static string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Dump(MermaidScene scene)
    {
        var lines = new List<string> { $"size {F(scene.Width)}x{F(scene.Height)}" };
        foreach (var n in scene.Nodes)
            lines.Add($"node {n.Node.Id}({n.Node.Shape.ToString().ToLowerInvariant()}) {F(n.X)},{F(n.Y)} {F(n.W)}x{F(n.H)}");
        foreach (var s in scene.Segments)
            lines.Add($"seg {F(s.X)},{F(s.Y)} {F(s.W)}x{F(s.H)}");
        // The PATH is in the fixture on purpose: it is a string both compilations build, and the
        // one place where a float formatted through a machine's culture would show up as `72,5`.
        foreach (var c in scene.Curves)
            lines.Add($"curve {F(c.X)},{F(c.Y)} {F(c.W)}x{F(c.H)} [{c.ViewBox}] {c.Path}");
        foreach (var a in scene.Arrows)
            lines.Add($"arrow {F(a.X)},{F(a.Y)} d{a.Direction}");
        foreach (var l in scene.Labels)
            lines.Add($"label \"{l.Text}\" {F(l.X)},{F(l.Y)}");
        return string.Join("\n", lines);
    }

    private static string SolveAndDump(string source)
    {
        var graph = MermaidParser.Parse(source);
        graph.Should().NotBeNull();
        return Dump(MermaidLayout.Solve(graph!));
    }

    [Fact]
    public void TheLayoutFixture_IsWhatBothCompilationsProduce()
    {
        var actual = "== flowchart ==\n" + SolveAndDump(Flowchart)
            + "\n== flowchart-lr ==\n" + SolveAndDump(FlowchartLr)
            + "\n== sequence ==\n" + SolveAndDump(Sequence) + "\n";

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_MERMAID_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
            File.WriteAllText(FixturePath, actual);
        }

        File.Exists(FixturePath).Should().BeTrue(
            "the pinned layout fixture must exist — regenerate with EQ_UPDATE_MERMAID_FIXTURE=1");
        File.ReadAllText(FixturePath).Should().Be(actual,
            "the C# layout drifted from the pinned fixture — if intended, regenerate it "
            + "with EQ_UPDATE_MERMAID_FIXTURE=1 (the vitest half must keep passing unchanged)");
    }

    // ---- Parser semantics ----------------------------------------------------------------------

    [Fact]
    public void AChain_DeclaresNodesAndEdges_AndShapesUpgradeBareMentions()
    {
        var graph = MermaidParser.Parse("graph LR\n  A --> B\n  B{Ready?} -->|go| C((Ship))")!;
        graph.Vertical.Should().BeFalse();
        graph.Nodes.Select(n => $"{n.Id}:{n.Shape}").Should().Equal("A:Rect", "B:Diamond", "C:Circle");
        graph.Nodes[1].Label.Should().Be("Ready?", "the shaped declaration upgrades the bare mention");
        graph.Edges.Select(e => $"{e.From}>{e.To}:{e.Label}").Should().Equal("A>B:", "B>C:go");
    }

    [Fact]
    public void AnOpenLink_CarriesNoArrow_AndADottedOneDrawsSolid()
    {
        var graph = MermaidParser.Parse("graph TD\n  A --- B\n  B -.-> C")!;
        graph.Edges[0].Arrow.Should().BeFalse();
        graph.Edges[1].Arrow.Should().BeTrue("dashed is a line STYLE the v1 fences, not a different link");
    }

    [Fact]
    public void QuotedLabels_LoseTheirQuotes()
    {
        var graph = MermaidParser.Parse("graph TD\n  A[\"Hello world\"] --> B")!;
        graph.Nodes[0].Label.Should().Be("Hello world");
    }

    [Fact]
    public void SubgraphLinesSkip_ButTheirStatementsParse_AndEndpointIsNotEnd()
    {
        var graph = MermaidParser.Parse(string.Join("\n",
        [
            "graph TD",
            "  subgraph cluster",
            "  A --> B",
            "  end",
            "  endpoint --> A",
        ]))!;
        graph.Nodes.Select(n => n.Id).Should().Equal("A", "B", "endpoint");
        graph.Edges.Should().HaveCount(2);
    }

    [Fact]
    public void SequenceMessages_AutoDeclareParticipants_AndKeepAliases()
    {
        var graph = MermaidParser.Parse(Sequence)!;
        graph.Kind.Should().Be(MermaidKind.Sequence);
        graph.Nodes.Select(n => $"{n.Id}={n.Label}").Should().Equal("U=User", "S=Server");
        graph.Messages.Select(m => $"{m.From}>{m.To}").Should().Equal("U>S", "S>U", "S>S");
        graph.Messages[1].Dashed.Should().BeTrue();
    }

    [Fact]
    public void CommentsStrip_AndUnknownGrammars_ReturnNull()
    {
        MermaidParser.Parse("graph TD %% flow\n  A --> B %% edge")!.Nodes.Should().HaveCount(2);
        MermaidParser.Parse("gantt\n  title Plan").Should().BeNull("gantt is outside the v1 subset");
        MermaidParser.Parse("classDiagram\n  A <|-- B").Should().BeNull();
        MermaidParser.Parse("graph BT\n  A --> B").Should().BeNull("BT is not spoken yet");
        MermaidParser.Parse("").Should().BeNull();
    }

    [Fact]
    public void ABackEdge_RoutesUpward_WithAnUpArrowhead()
    {
        var graph = MermaidParser.Parse(Flowchart)!;
        var scene = MermaidLayout.Solve(graph);
        scene.Arrows.Should().Contain(a => a.Direction == 2, "D --> A points back up the flow");
    }

    [Fact]
    public void EveryCoordinate_IsAWholeNumberOrAnExactHalf()
    {
        // The parity contract, asserted directly: nothing in a solved scene carries a fraction a
        // float and a double could disagree on.
        var scene = MermaidLayout.Solve(MermaidParser.Parse(Flowchart)!);
        var values = new List<float>();
        foreach (var n in scene.Nodes) values.AddRange([n.X, n.Y, n.W, n.H]);
        foreach (var s in scene.Segments) values.AddRange([s.X, s.Y, s.W, s.H]);
        foreach (var a in scene.Arrows) values.AddRange([a.X, a.Y]);
        foreach (var l in scene.Labels) values.AddRange([l.X, l.Y]);
        values.Should().OnlyContain(v => v * 2 == MathF.Floor(v * 2),
            "every coordinate must be a whole number or an exact half");
    }
}
