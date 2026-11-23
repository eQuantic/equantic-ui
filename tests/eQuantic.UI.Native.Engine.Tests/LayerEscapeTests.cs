using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Spreadsheets;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A layer's Escape waits for the surface the keyboard is in (#1181). A dialog, a sheet or a drawer around
/// a code editor or a spreadsheet closes on Escape only when the surface has nothing of its own to close,
/// as Flutter dismisses a route only for an Escape nothing nearer the focus took. The host asked the
/// layer's binding first, so Escape on an open completion list closed the dialog, and what was typed in it
/// went with it. The web twins are in <c>code-editor.spec.ts</c> and <c>sheet-surface.spec.ts</c>.
/// </summary>
public class LayerEscapeTests
{
    private static readonly CellRef B2 = new(1, 1);

    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 600, 400)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            SmoothScroll = false,
        };

    private static RealizeResult Settle(PhotonHost host)
    {
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder());
    }

    /// <summary>An editor over a line that offers words for Co, in a layer that logs its Escape, with
    /// "Co" typed on the line under it: the list is open.</summary>
    private static (PhotonHost Host, CodeEditor Editor, List<string> Log) ListOpenInADialog()
    {
        var log = new List<string>();
        var editor = new CodeEditor("var Column = 1; var ColorToken = 2;\n\n\n", "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = [new ListCompletionProvider(new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"))],
        };
        var host = Host(new Shortcut(editor, KeyChord.Escape, () => log.Add("dialog")));
        var region = Settle(host).CodeRegions.Single();
        var at = editor.Editor.CaretRect(new CodePosition(1, 0));
        host.PressDown(region.Bounds.X + at.X + 1, region.Bounds.Y + at.Y + at.Height / 2);
        host.PressUp(region.Bounds.X + at.X + 1, region.Bounds.Y + at.Y + at.Height / 2);
        Settle(host);
        foreach (var c in "Co") host.TextInput(c.ToString());
        Settle(host);
        editor.Editor.Completion.IsOpen.Should().BeTrue("a word typed opens the list");
        return (host, editor, log);
    }

    [Fact]
    public void Escape_ClosesTheCompletionList_BeforeADialogAroundTheEditor()
    {
        var (host, editor, log) = ListOpenInADialog();

        host.KeyDown("Escape").Should().BeTrue();

        log.Should().BeEmpty("the list is nearer the keyboard than the dialog");
        editor.Editor.Completion.IsOpen.Should().BeFalse();
        editor.Editor.Document.Line(1).Should().Be("Co", "what was typed stays");
        Settle(host);
        host.KeyDown("Escape").Should().BeTrue();
        log.Should().Equal(["dialog"], "with nothing over the code, Escape is the dialog's");
    }

    [Fact]
    public void Escape_LeavesTheTabTrapArmed_WhenTheListTookIt()
    {
        var (host, editor, _) = ListOpenInADialog();

        host.KeyDown("Escape");

        editor.Editor.TabMovesFocus.Should().BeFalse("the list took the Escape, which released nothing");
    }

    [Fact]
    public void AnAppsOwnChord_StaysTheApps_InsideTheEditor()
    {
        var palette = 0;
        var editor = new CodeEditor("var x = 1;", "csharp") { ShowLineNumbers = false };
        var host = Host(new Shortcut(editor, new KeyChord("k", KeyModifiers.Command), () => palette++));
        var region = Settle(host).CodeRegions.Single();
        host.PressDown(region.Bounds.X + 4, region.Bounds.Y + 4);
        host.PressUp(region.Bounds.X + 4, region.Bounds.Y + 4);
        Settle(host);

        host.KeyDown("k", KeyModifiers.Command).Should().BeTrue();

        palette.Should().Be(1, "only Escape waits for the surface: an app's chord answers first, as it did");
    }

    /// <summary>A spreadsheet in a layer that logs its Escape, with B2 being edited.</summary>
    private static (PhotonHost Host, SheetController Sheet, List<string> Log) EditInADialog()
    {
        var log = new List<string>();
        var sheet = new SheetController(rows: 40, cols: 6);
        var host = Host(new Shortcut(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            KeyChord.Escape, () => log.Add("dialog")));
        Settle(host);
        var grid = sheet.Grid;
        var x = 1 + grid.HeaderWidth + B2.Col * 96f + 48f;
        var y = 1 + grid.HeaderHeight + B2.Row * 28f + 14f;
        host.PressDown(x, y);
        host.PressUp(x, y);
        Settle(host);
        host.TextInput("4");
        Settle(host);
        sheet.Editing.Should().BeTrue("a character typed at rest begins an entry");
        return (host, sheet, log);
    }

    [Fact]
    public void Escape_CancelsACellsEdit_BeforeADialogAroundTheSheet()
    {
        var (host, sheet, log) = EditInADialog();

        host.KeyDown("Escape").Should().BeTrue();

        log.Should().BeEmpty("the edit is nearer the keyboard than the dialog");
        sheet.Editing.Should().BeFalse("Escape cancels the edit, as Excel's does");
        sheet.Document.GetCell(B2).Should().BeEmpty("a cancelled edit writes nothing");
        Settle(host);
        host.KeyDown("Escape").Should().BeTrue();
        log.Should().Equal(["dialog"], "with nothing being edited, Escape is the dialog's");
    }

    [Fact]
    public void TheBack_StillClosesTheLayer_WhileACellIsEdited()
    {
        var (host, sheet, log) = EditInADialog();

        host.Back().Should().BeTrue();

        log.Should().Equal(["dialog"], "neither an edit nor a focus ring takes the platform's back");
        sheet.Editing.Should().BeTrue();
    }

    // ---- the models' answer is what their keys then do -----------------------------------------------
    // A host decides between the surface and the page by ClaimsEscape, and hands the key to the one it
    // chose: an answer HandleKey then contradicts either closes a dialog over something open, or swallows
    // an Escape the dialog was owed.

    public static TheoryData<KeyboardConvention> Conventions => [KeyboardConvention.Standard, KeyboardConvention.Apple];

    private static void AnswersAsItsEscape(ICodeSurfaceModel model, KeyboardConvention convention, bool expected, string state)
    {
        var claims = model.ClaimsEscape(convention);
        claims.Should().Be(expected, state);
        model.HandleKey("Escape", KeyModifiers.None, convention, null).Should().Be(claims, $"{state}: what Escape then does");
    }

    private static void AnswersAsItsEscape(ISheetSurfaceModel model, KeyboardConvention convention, bool expected, string state)
    {
        var claims = model.ClaimsEscape(convention);
        claims.Should().Be(expected, state);
        model.HandleKey("Escape", KeyModifiers.None, convention, null).Should().Be(claims, $"{state}: what Escape then does");
    }

    [Theory]
    [MemberData(nameof(Conventions))]
    public void TheCodeEditor_ClaimsEscape_ExactlyWhenItsEscapeDoesSomething(KeyboardConvention convention)
    {
        AnswersAsItsEscape(new CodeEditorController("one one"), convention, false, "nothing over the code");

        var carets = new CodeEditorController("one one") { Selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 3)) };
        carets.Execute(CodeCommand.AddNextOccurrence);
        AnswersAsItsEscape(carets, convention, true, "a second caret");

        var goTo = new CodeEditorController("one\ntwo");
        goTo.Execute(CodeCommand.GoToLine);
        AnswersAsItsEscape(goTo, convention, true, "the go-to field open");

        var snippet = new CodeEditorController("");
        snippet.InsertSnippet(CodeSnippet.Parse("for (var ${1:i} = 0; $1 < ${2:count}; $1++)"));
        AnswersAsItsEscape(snippet, convention, true, "a snippet being walked");

        var rebound = new CodeEditorController("one one") { Selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 3)) };
        rebound.KeyBindings.Add(new CodeKeyBinding(KeyChord.Escape, CodeCommand.None));
        rebound.Execute(CodeCommand.AddNextOccurrence);
        AnswersAsItsEscape(rebound, convention, false, "an app that gave Escape to the host");
    }

    [Theory]
    [MemberData(nameof(Conventions))]
    public void TheSheet_ClaimsEscape_ExactlyWhenItsEscapeDoesSomething(KeyboardConvention convention)
    {
        AnswersAsItsEscape(new SheetController(rows: 4, cols: 4), convention, false, "at rest");

        var editing = new SheetController(rows: 4, cols: 4);
        editing.BeginEdit("4", SheetEditMode.Enter);
        AnswersAsItsEscape(editing, convention, true, "a cell being edited");

        var adding = new SheetController(rows: 4, cols: 4);
        adding.HandleKey("F8", KeyModifiers.Shift, convention, null);
        AnswersAsItsEscape(adding, convention, true, "adding ranges");

        var bar = new SheetController(rows: 4, cols: 4);
        AnswersAsItsEscape(bar.FormulaBar, convention, false, "the formula bar at rest");
        bar.BeginEdit("4", SheetEditMode.Enter);
        AnswersAsItsEscape(bar.FormulaBar, convention, true, "the formula bar over an edit");

        var box = new SheetController(rows: 4, cols: 4);
        AnswersAsItsEscape(box.NameBox, convention, false, "the name box at rest");
        box.NameBox.FocusChanged(true);
        AnswersAsItsEscape(box.NameBox, convention, true, "the name box being typed into");
    }
}
