using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An editor and a grid exist in the server's HTML.
///
/// <para>
/// Both shipped in August 2026 and lowered to NOTHING until this file: `WebRealizer` had no arm for
/// either, so `_ => null` caught them and SSR wrote an empty span where the browser draws a whole
/// editor. Build green, tests green, and a document with a hole in it for every crawler and every
/// reader without JavaScript.
/// </para>
///
/// <para>
/// Found by the vocabulary coverage pin the day it grew to cover the web realizer — which is the
/// argument for that pin in one sentence. Nobody noticed in a month of using both components,
/// because the browser fills the hole a quarter of a second later.
/// </para>
/// </summary>
public class SurfaceSsrTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Render(VisualNode node) =>
        HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme)!.Render());

    private static CodeSurface Code(string text) =>
        new(new Text(text, TypeRole.BodyM), new CodeEditorController(text));

    private static SheetSurface Sheet(string text) =>
        new(new Text(text, TypeRole.BodyM), new SheetController(rows: 3, cols: 3));

    /// <summary>
    /// THE DEFECT, in the form it was proved in: the whole editor was one empty span.
    /// </summary>
    [Fact]
    public void ACodeSurface_IsNotAnEmptySpan()
    {
        var html = Render(Code("readonly int answer = 42;"));

        html.Should().NotBe("<span></span>");
        html.Should().Contain("readonly int answer = 42;",
            "the code is the document's content — a reader without scripts, and every crawler, gets "
            + "only what the server wrote");
    }

    [Fact]
    public void ASheetSurface_IsNotAnEmptySpan()
    {
        var html = Render(Sheet("Q3 revenue"));

        html.Should().NotBe("<span></span>");
        html.Should().Contain("Q3 revenue");
    }

    /// <summary>
    /// The surface announces what it IS. An editor that reaches assistive technology as an unlabelled
    /// div is the same silence one layer up from the empty span.
    /// </summary>
    [Fact]
    public void ACodeSurface_ArrivesAsAMultilineTextbox()
    {
        var html = Render(new CodeSurface(new Text("x", TypeRole.BodyM), new CodeEditorController("x"))
        {
            Label = "Program.cs",
        });

        html.Should().Contain("role=\"textbox\"");
        html.Should().Contain("aria-multiline=\"true\"");
        html.Should().Contain("aria-label=\"Program.cs\"");
        html.Should().Contain("tabindex=\"0\"", "an editor is reachable by keyboard before hydration");
    }

    [Fact]
    public void ASheetSurface_ArrivesAsAGrid()
    {
        var html = Render(new SheetSurface(new Text("x", TypeRole.BodyM), new SheetController(2, 2))
        {
            Label = "Budget",
        });

        html.Should().Contain("role=\"grid\"");
        html.Should().Contain("aria-label=\"Budget\"");
        html.Should().Contain("tabindex=\"0\"");
    }

    /// <summary>
    /// What the server deliberately does NOT write, pinned so the omissions stay deliberate.
    ///
    /// <para>
    /// The caret and the selection band are where the READER is, not what the document says. A caret
    /// rendered into markup is a caret in the wrong place the moment anyone types, and it would
    /// disagree with the client's on the first frame.
    /// </para>
    /// </summary>
    [Fact]
    public void TheServerWritesNoCaretAndNoSelection()
    {
        var html = Render(Code("let x = 1"));

        html.Should().NotContain("eq-code-caret", "a caret is live state, not content");
        html.Should().NotContain("eq-code-selection");
    }

    /// <summary>
    /// The other deliberate difference: the client stamps `data-eq-code` with the node's PATH so
    /// anything running after a render can find the surface again. The web realizer lowers a tree,
    /// not a laid-out one, and has no path to stamp — hydration adds the attribute.
    /// </summary>
    [Fact]
    public void TheServerStampsNoPath()
    {
        Render(Code("let x = 1")).Should().NotContain("data-eq-code");
    }
}
