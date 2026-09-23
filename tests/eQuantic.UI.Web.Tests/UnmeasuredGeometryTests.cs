using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the server could not MEASURE, the client draws again.
/// <para>
/// The server has no font, so a component that asks how wide its text is gets 0 there, and a
/// component whose geometry IS text geometry is built on zeros: a code block's gutter came out
/// 12px wide, the padding and nothing else. Hydration keeps the server's markup, so the client
/// adopted that gutter and kept it for as long as the page lived (measured on the dashboard's
/// <c>/markdown</c>). The realizer now counts the questions (<see cref="FontlessMeasurer"/>) and
/// marks the component that asked, and hydration draws a marked subtree instead of adopting it.
/// </para>
/// </summary>
public class UnmeasuredGeometryTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly string Mark = "data-" + WebLoweringVisitor.UnmeasuredMark;

    /// <summary>The SSR path, with a sink, as <c>ServerRenderingService</c> runs it.</summary>
    private static HtmlNode Lower(VisualNode node) =>
        WebRealizer.Lower(node, Theme, 1f, new StyleSink()).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node) =>
        new[] { node }.Concat(node.Children.SelectMany(Walk));

    private static string TextOf(HtmlNode node) =>
        node.TextContent ?? string.Concat(node.Children.Select(TextOf));

    /// <summary>Asks for one width while it builds, and builds nothing that asks.</summary>
    private sealed class AsksForAWidth : UiComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var width = context.MonoAdvance(context.Theme.Type(TypeRole.BodyM));
            return new Text($"advance {width}", TypeRole.BodyM);
        }
    }

    /// <summary>Asks for nothing itself, and builds a code block, which does.</summary>
    private sealed class HoldsACodeBlock : UiComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text("above the code", TypeRole.BodyM));
            column.Add(new CodeBlock("var total = 1;", "csharp"));
            return column;
        }
    }

    [Fact]
    public void ACodeBlock_IsMarkedForTheClientToDraw_AndStillCarriesItsCode()
    {
        var root = Lower(new CodeBlock("var total = 1;", "csharp"));

        root.Attributes.Should().ContainKey(Mark, "its gutter and its columns were built on widths of 0");
        TextOf(root).Should().Contain("var total = 1;",
            "the mark changes who DRAWS it, not what the server's HTML says to a crawler");
    }

    [Fact]
    public void AComponentThatAsksNothing_IsAdoptedAsItIs()
    {
        Walk(Lower(new Button("Save"))).Should().NotContain(node => node.Attributes.ContainsKey(Mark));
    }

    [Fact]
    public void AComponentThatAsked_IsMarkedWhateverItBuilt()
    {
        Lower(new AsksForAWidth()).Attributes.Should().ContainKey(Mark,
            "the question was asked in its own Build, even though the text it built asks nothing");
    }

    /// <summary>
    /// The mark lands on the component that ASKED, not on everything around it: the client draws
    /// the least it has to, and adopts the rest. A component's children build while its own tree
    /// lowers, which is after its Build has been counted, so their questions are theirs.
    /// </summary>
    [Fact]
    public void OnlyTheComponentThatAsked_IsMarked_NotTheOneAroundIt()
    {
        var root = Lower(new HoldsACodeBlock());

        var marked = Walk(root).Where(node => node.Attributes.ContainsKey(Mark)).ToList();
        root.Attributes.Should().NotContainKey(Mark, "the column around the code block measured nothing");
        marked.Should().ContainSingle();
        TextOf(marked[0]).Should().Contain("var total = 1;").And.NotContain("above the code");
    }

    /// <summary>
    /// The editor measures its code through the block's metrics, so it is marked too, and its
    /// surface now WRITES: the code the server's HTML had a hole for.
    /// </summary>
    [Fact]
    public void ACodeEditor_ArrivesWithItsCode_AndIsMarkedForTheClientToDraw()
    {
        var root = Lower(new CodeEditor("var total = 1;", "csharp"));

        root.Attributes.Should().ContainKey(Mark);
        TextOf(root).Should().Contain("var total = 1;");
        Walk(root).Should().Contain(node => node.Tag == "textarea",
            "the surface writes the input the client types through");
    }

    /// <summary>
    /// The CROSS-PIN: the client reads the mark by the same name the server writes it. Both are a
    /// string, and a string that drifts on one side fails nothing at all: the server would mark,
    /// the client would adopt, and the gutter would be 12px again with every suite green.
    /// </summary>
    [Fact]
    public void TheClientReadsTheMarkByTheNameTheServerWritesIt()
    {
        var reconciler = File.ReadAllText(Path.Combine(RepoRoot(),
            "src", "eQuantic.UI.Runtime", "src", "dom", "reconciler.ts"));

        reconciler.Should().Contain($"export const UNMEASURED_MARK = '{Mark}';");
    }

    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
}
