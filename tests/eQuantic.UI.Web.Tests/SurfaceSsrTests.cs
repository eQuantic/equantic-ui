using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Spreadsheets;
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

    /// <summary>
    /// THE SSR PATH, which means WITH A SINK. `ServerRenderingService` arms a `StyleSink`, so the
    /// style becomes atomic classes; the no-sink overload leaves it inline and is a different thing
    /// to measure. The first version of this helper used the no-sink one, so these tests could have
    /// passed while the server's atomised output diverged from the client's. Found in review.
    /// </summary>
    private static string Render(VisualNode node) =>
        HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme, 1f, new StyleSink())!.Render());

    private static SheetSurface Sheet(string text) =>
        new(new Text(text, TypeRole.BodyM), new SheetController(rows: 3, cols: 3));

    /// <summary>A code surface over a model with a known grid, its caret where a test puts it.</summary>
    private static CodeSurface Code(string text, eQuantic.UI.Code.CodePosition caret)
    {
        var model = new eQuantic.UI.Code.CodeEditorController(text, eQuantic.UI.Code.CodeLanguages.CSharp)
        {
            Grid = new eQuantic.UI.Code.CodeGrid(new Point(12, 12), new Size(8, 18)),
        };
        model.Selection = new eQuantic.UI.Code.CodeRange(caret);
        return new CodeSurface(new Text(text, TypeRole.BodyM), model) { Label = "Program.cs" };
    }

    /// <summary>
    /// The code editor's surface WRITES now. It was the one node this realizer answered null for,
    /// so the server's HTML had a hole where the editor goes: no code for a crawler, none for a
    /// reader without JavaScript, and a flash of nothing before the client mounted. Its geometry
    /// is a draft the client draws again (<see cref="UnmeasuredGeometryTests"/>), and its code is not.
    /// </summary>
    [Fact]
    public void ACodeSurface_ArrivesWithItsCode()
    {
        var html = Render(Code("var x = 1;", eQuantic.UI.Code.CodePosition.Start));

        html.Should().Contain("var x = 1;");
        html.Should().Contain("eq-code-surface");
    }

    /// <summary>
    /// …and with the same children the client builds: its caret where the MODEL puts it, and the
    /// input the keyboard types through, at the caret, in the client's spellings (TokenCss.Px
    /// beside its px()). The client draws the editor again rather than adopting it, so this is not
    /// what keeps hydration whole: it is what keeps the two shapes one shape, for the day the
    /// server can measure.
    /// </summary>
    [Fact]
    public void ItsCaretAndItsInputAreWrittenWhereTheClientWritesThem()
    {
        var html = Render(Code("one\ntwo", new eQuantic.UI.Code.CodePosition(1, 2)));

        // The grid the model was handed: 12 + 2 × 8 across, 12 + 1 × 18 down.
        html.Should().Contain("position:absolute;left:28px;top:30px;width:2px;height:18px;");
        html.Should().Contain("<textarea");
        html.Should().Contain("left:28px;top:30px;height:18px;");
        html.Should().Contain("aria-label=\"Program.cs\"");
        html.Should().Contain("spellcheck=\"false\"");
    }

    /// <summary>
    /// What a surface offers at its caret is the input's LISTBOX (#297), written as the client
    /// writes it (TS twin: lowerCodeSurface): the list stands at its origin over the code, its rows
    /// are numbered options, and the input says it completes from that list and points at the row
    /// the keyboard is on.
    /// </summary>
    [Fact]
    public void WhatItOffers_IsTheInputsListbox()
    {
        var page = new Column(gap: 0);
        page.Add(new Pressable(new Text("ColorToken", TypeRole.LabelSmall), () => { })
            { Role = PressableRole.Option, Selected = false, CanRequestFocus = false });
        page.Add(new Pressable(new Text("Column", TypeRole.LabelSmall), () => { })
            { Role = PressableRole.Option, Selected = true, CanRequestFocus = false });
        var surface = Code("one\nCo", new eQuantic.UI.Code.CodePosition(1, 2));
        surface = new CodeSurface(surface.Child, surface.Model)
        {
            Label = "Program.cs",
            Options = page,
            OptionsOrigin = new Point(28, 48),
            HighlightedOption = 1,
        };

        var html = Render(surface);

        var listId = System.Text.RegularExpressions.Regex.Match(html, "id=\"(eq-options-[^\"]+)\"").Groups[1].Value;
        listId.Should().NotBeEmpty("the list carries the id the input names");
        html.Should().Contain("role=\"listbox\"");
        html.Should().Contain("position:absolute;left:28px;top:48px;white-space:normal;",
            "the list's prose wraps under the surface's pre, which keeps the code on its line");
        html.Should().Contain($"id=\"{listId}-0\"").And.Contain($"id=\"{listId}-1\"");
        html.Should().Contain("aria-autocomplete=\"list\"");
        html.Should().NotContain("aria-expanded", "ARIA allows it on a combobox, not on the textbox a textarea is");
        html.Should().Contain($"aria-controls=\"{listId}\"");
        html.Should().Contain($"aria-activedescendant=\"{listId}-1\"");
        html.Should().Contain("tabindex=\"-1\"", "a row is no Tab stop: the arrows walk the list from the code");
    }

    /// <summary>
    /// What a surface explains over its code (#1005), a card, is written as the client writes it (TS
    /// twin: lowerCodeSurface): each note a block at its start and top in the surface's coordinates,
    /// over the input and under the list. A card shows as a pointer rests or a key asks, so a page
    /// rarely carries one from the server, and one that does keeps the client's shape.
    /// </summary>
    [Fact]
    public void WhatItExplains_IsANoteWhereTheClientWritesIt()
    {
        var surface = Code("one\ntwo", new eQuantic.UI.Code.CodePosition(1, 2));
        surface = new CodeSurface(surface.Child, surface.Model)
        {
            Label = "Program.cs",
            Notes = [new Positioned(new Text("About one.", TypeRole.LabelSmall), top: 30, start: 28)],
        };

        var html = Render(surface);

        var note = html.IndexOf("eq-code-note", StringComparison.Ordinal);
        note.Should().BeGreaterThan(html.IndexOf("<textarea", StringComparison.Ordinal), "the note is drawn over the input");
        // The caret stands at the same place, so the wrapper is told apart by what follows its top: its
        // prose wraps under the surface's pre (found by Copilot reviewing #1008).
        html.Should().Contain("position:absolute;left:28px;top:30px;white-space:normal;");
        html[note..].Should().Contain("About one.");
    }

    [Fact]
    public void WithNothingOffered_TheInputNamesNoList()
    {
        var html = Render(Code("one", eQuantic.UI.Code.CodePosition.Start));

        html.Should().NotContain("aria-autocomplete").And.NotContain("aria-activedescendant")
            .And.NotContain("role=\"listbox\"");
    }

    [Fact]
    public void ItsCodeKeepsItsLayersUnderTheCaret()
    {
        var sink = new StyleSink();
        WebRealizer.Lower(Code("x", eQuantic.UI.Code.CodePosition.Start), Theme, 1f, sink)!.Render();

        sink.Css.Should().Contain("isolation:isolate");
        sink.Css.Should().Contain("cursor:text");
        sink.Css.Should().Contain("user-select:none");
    }


    [Fact]
    public void ASheetSurface_IsNotAnEmptySpan()
    {
        var html = Render(Sheet("Q3 revenue"));

        html.Should().NotBe("<span></span>");
        html.Should().Contain("Q3 revenue");
    }


    [Fact]
    public void ASheetSurface_ArrivesAsTheFieldThatTakesItsKeys_SayingWhatTheGridSays()
    {
        var html = Render(new SheetSurface(new Text("x", TypeRole.BodyM), new SheetController(2, 2))
        {
            Label = "Budget",
            Announcement = "B3, 14,02",
        });

        // The table a sheet holds is the grid a reader walks (#1081), and this one holds none.
        html.Should().NotContain("role=\"grid\"");
        html.Should().Contain("aria-label=\"Budget\"");
        html.Should().Contain("tabindex=\"0\"");
        html.Should().Contain("role=\"status\"").And.Contain("aria-live=\"polite\"").And.Contain("B3, 14,02");
    }

    [Fact]
    public void ASpreadsheet_ArrivesAsAGridOfHeadersAndCells()
    {
        var sheet = new SheetController(30, 6);
        sheet.Document.SetCell(new CellRef(2, 1), "14,02");

        var html = Render(new Spreadsheet(sheet));

        html.Should().Contain("role=\"grid\"").And.Contain("aria-label=\"Spreadsheet\"")
            .And.Contain("aria-rowcount=\"31\"").And.Contain("aria-colcount=\"7\"")
            .And.Contain("role=\"columnheader\"").And.Contain("role=\"rowheader\"")
            .And.Contain("role=\"gridcell\"").And.Contain("role=\"row\"")
            .And.Contain("aria-current=\"true\"").And.Contain("A1, blank");
    }




    /// <summary>
    /// The inherited flex axis travels THROUGH the sheet, and this is the case that can see it.
    ///
    /// <para>
    /// Both other tests put a `Text` inside, which lowers the same whatever the axis is — so the
    /// forwarding could regress to `null` and they would stay green. A `Spacer` is the node that
    /// cares: with no axis it lowers to nothing on the server while the browser renders it, which
    /// is a tree the two sides disagree about at hydration. Found in review.
    /// </para>
    /// </summary>
    [Fact]
    public void TheInheritedAxisReachesTheSheetsChild()
    {
        var row = new Row(gap: Space.S2);
        row.Add(new SheetSurface(new Spacer(), new SheetController(2, 2)));

        var html = Render(row);

        html.Should().Contain("eq-sheet-surface");
        // The spacer is a node that EXISTS or does not: with no axis, `LowerSpacer` returns null and
        // the grid comes out empty. Asserted by presence rather than by a declaration, because the
        // declaration is atomised into a class name nobody should have to predict.
        html.Should().Contain("aria-hidden=\"true\"",
            "a Spacer inside a Row lowers to a decorative div \u2014 and only reaches the server at all "
            + "because the axis was forwarded through the sheet");
    }
}
