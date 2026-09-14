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

    private static SheetSurface Sheet(string text) =>
        new(new Text(text, TypeRole.BodyM), new SheetController(rows: 3, cols: 3));


    [Fact]
    public void ASheetSurface_IsNotAnEmptySpan()
    {
        var html = Render(Sheet("Q3 revenue"));

        html.Should().NotBe("<span></span>");
        html.Should().Contain("Q3 revenue");
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



}
