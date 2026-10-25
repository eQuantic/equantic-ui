using System.Diagnostics;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Spreadsheets;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a spreadsheet of 100 000 rows by 100 columns costs on Photon: a keystroke, which builds the
/// sheet again and with it the window of cells, and a scroll step inside the cells already built, which
/// builds nothing. The ceilings follow the harness's rule (see <see cref="PerfHarnessTests"/>): an order
/// of magnitude loose, because CI machines are shared, and still far below what a sheet that built its
/// rows, or its columns, would cost.
/// </summary>
[Collection(PerfHarnessCollection.Name)]
public class SpreadsheetPerfTests
{
    private const double KeystrokeCeilingMs = 60;
    private const double StepCeilingMs = 12;

    private readonly ITestOutputHelper _output;

    public SpreadsheetPerfTests(ITestOutputHelper output) => _output = output;

    private static (PhotonHost Host, SheetController Sheet) Open()
    {
        var sheet = new SheetController(rows: 100_000, cols: 100);
        for (var row = 0; row < 200; row++)
            for (var column = 0; column < 4; column++)
                sheet.Document.SetCell(new CellRef(row, column), $"r{row}c{column}");
        var host = new PhotonHost(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 1440, 900)
        {
            SmoothScroll = false,
            ReducedMotion = true,
        };
        for (var t = 0; t < 4; t++) host.RenderFrame(new DisplayListBuilder(), t * 16);
        return (host, sheet);
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node)
            foreach (var below in Walk(child))
                yield return below;
    }

    /// <summary>The cells the frame holds, by their configuration: the same objects when nothing built.</summary>
    private static List<TableViewCell> Cells(PhotonHost host) =>
        Walk(host.LastFrame!.Root).Select(n => n.Source).OfType<TableViewCell>().Where(c => !c.Mark).ToList();

    [Fact]
    public void AKeystroke_BuildsTheWindowAgain_AndStaysInsideTheAlarm()
    {
        var (host, sheet) = Open();
        var region = host.LastFrame!.TableRegions.Single();
        var at = region.Bounds.Center;
        host.PressDown(at.X, at.Y);
        host.PressUp(at.X, at.Y);
        var time = 100f;
        for (var i = 0; i < 5; i++)
        {
            host.KeyDown(i % 2 == 0 ? "ArrowRight" : "ArrowLeft");
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }

        const int keys = 40;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < keys; i++)
        {
            // Across and back inside the window: each moves the active cell and builds the sheet again.
            host.KeyDown(i % 2 == 0 ? "ArrowRight" : "ArrowLeft").Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }
        var each = clock.Elapsed.TotalMilliseconds / keys;
        var cells = Cells(host).Count;

        _output.WriteLine($"a keystroke at 100,000 × 100: {each:F2} ms, {cells} cells in the window (alarm {KeystrokeCeilingMs} ms)");
        cells.Should().BeLessThan(1_500, "the window and its overscan, never a row or a column whole");
        each.Should().BeLessThan(KeystrokeCeilingMs, "a keystroke builds a screenful");
        sheet.Editing.Should().BeFalse();
    }

    [Fact]
    public void ACharacterTypedIntoAnEntry_StaysInsideTheAlarm()
    {
        var (host, sheet) = Open();
        var region = host.LastFrame!.TableRegions.Single();
        var at = region.Bounds.Center;
        host.PressDown(at.X, at.Y);
        host.PressUp(at.X, at.Y);
        var time = 100f;
        for (var i = 0; i < 5; i++)
        {
            host.TextInput("w");
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }

        const int keys = 40;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < keys; i++)
        {
            // Each character grows the entry, measures its line and builds the sheet with its editor.
            host.TextInput(i % 2 == 0 ? "a" : "b").Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }
        var each = clock.Elapsed.TotalMilliseconds / keys;

        _output.WriteLine($"a character into an entry at 100,000 × 100: {each:F2} ms, {sheet.Entry.Document.Text.Length} characters, the editor over {sheet.EditorRange.ColCount} columns (alarm {KeystrokeCeilingMs} ms)");
        sheet.Editing.Should().BeTrue();
        sheet.Entry.Document.Text.Length.Should().Be(45);
        each.Should().BeLessThan(KeystrokeCeilingMs, "a character builds a screenful and its editor");
    }

    [Fact]
    public void AScrollStepInsideTheBuiltCells_BuildsNothing_AndStaysInsideTheAlarm()
    {
        var (host, _) = Open();
        var region = host.LastFrame!.TableRegions.Single();
        var at = region.Bounds.Center;
        var time = 100f;
        for (var i = 0; i < 5; i++)
        {
            host.ScrollBy(at.X, at.Y, 0, 4, stepped: false);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            host.ScrollBy(at.X, at.Y, 0, -4, stepped: false);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }
        var before = Cells(host);

        const int steps = 40;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < steps; i++)
        {
            // Two points either way: well inside the three rows and columns of overscan.
            host.ScrollBy(at.X, at.Y, i % 2 == 0 ? 2 : -2, i % 2 == 0 ? 2 : -2, stepped: false).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }
        var each = clock.Elapsed.TotalMilliseconds / steps;
        var after = Cells(host);

        _output.WriteLine($"a scroll step inside the window at 100,000 × 100: {each:F2} ms, {after.Count} cells (alarm {StepCeilingMs} ms)");
        after.Should().HaveSameCount(before);
        after.Zip(before).Should().OnlyContain(pair => ReferenceEquals(pair.First, pair.Second),
            "a step inside the built cells moves them and builds none, the sheet included");
        each.Should().BeLessThan(StepCeilingMs, "a step inside the window lays out what is in view");
    }
}
