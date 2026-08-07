using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// In-cell editing, Excel's quick-entry rhythm, conducted through the host: typing REPLACES the
/// cell with a fresh draft, Enter commits and steps down, Escape discards and the cell keeps what
/// it had, F2/double-click edit the current value in place, and the arrows commit-then-move. The
/// document never changes until commit — the draft lives in the shared controller, so the web
/// half edits identically.
/// </summary>
public class SheetEditingTests
{
    private sealed class Page : Primitives.StatefulComponent
    {
        public SheetController Sheet { get; } = new(rows: 50, cols: 8);

        public override VisualNode Build(ComponentContext context) =>
            new SheetSurface(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }), Sheet)
            {
                OnChanged = () => SetState(() => { }),
            };
    }

    private static (PhotonHost Host, SheetController Sheet) Open()
    {
        var page = new Page();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 600, 400);
        host.RenderFrame(new DisplayListBuilder());
        host.PressDown(48, 14);
        host.PressUp(48, 14);
        return (host, page.Sheet);
    }

    [Fact]
    public void Typing_StartsAFreshDraft_AndEnterCommitsSteppingDown()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(new CellRef(0, 0), "old");

        host.TextInput("4");
        host.TextInput("2");

        sheet.Editing.Should().BeTrue();
        sheet.Draft.Should().Be("42");
        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("old", "the document waits for commit");

        host.KeyDown("Enter", KeyModifiers.None);

        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("42");
        sheet.ActiveCell.Should().Be(new CellRef(1, 0), "Enter commits and steps down");
        sheet.Editing.Should().BeFalse();
    }

    [Fact]
    public void Escape_Discards_AndTheCellKeepsWhatItHad()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(new CellRef(0, 0), "kept");

        host.TextInput("x");
        host.KeyDown("Escape", KeyModifiers.None);

        sheet.Editing.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("kept");
    }

    [Fact]
    public void F2_EditsTheCurrentValueInPlace()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(new CellRef(0, 0), "price");

        host.KeyDown("F2", KeyModifiers.None);

        sheet.Editing.Should().BeTrue();
        sheet.Draft.Should().Be("price", "F2 opens the value, typing appends");

        host.TextInput("s");
        host.KeyDown("Enter", KeyModifiers.None);
        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("prices");
    }

    [Fact]
    public void Arrows_CommitThenMove_TheQuickEntryRhythm()
    {
        var (host, sheet) = Open();

        host.TextInput("a");
        host.KeyDown("ArrowRight", KeyModifiers.None);

        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("a");
        sheet.ActiveCell.Should().Be(new CellRef(0, 1));
        sheet.Editing.Should().BeFalse();
    }

    [Fact]
    public void Backspace_ErasesFromTheDraft()
    {
        var (host, sheet) = Open();

        host.TextInput("ab");
        host.KeyDown("Backspace", KeyModifiers.None);

        sheet.Draft.Should().Be("a");
    }

    [Fact]
    public void DoubleClick_EditsInPlace_AndClickingAwayLandsTheDraft()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(new CellRef(0, 0), "here");

        host.PressDown(48, 14, clickCount: 2);
        host.PressUp(48, 14);
        sheet.Editing.Should().BeTrue();
        sheet.Draft.Should().Be("here");

        host.TextInput("!");
        host.PressDown(48 + 96, 14);   // click the next cell over
        host.PressUp(48 + 96, 14);

        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("here!", "clicking away lands the draft");
        sheet.ActiveCell.Should().Be(new CellRef(0, 1));
    }

    [Fact]
    public void TheDraft_UndoesAsOneStep()
    {
        var (host, sheet) = Open();

        host.TextInput("abc");
        host.KeyDown("Enter", KeyModifiers.None);
        host.KeyDown("z", KeyModifiers.Command);

        sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("", "one commit, one undo");
    }
}
