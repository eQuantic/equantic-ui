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
/// builds nothing.
/// <para>
/// The guard is the WORK, counted, as the table's is (#1397, #1915): the cells a step builds and the layout nodes
/// its frame walks, which no runner's load can move. Beside it, a loose time alarm read on the FASTEST step or key
/// rather than the mean: a shared runner slows some of them and never makes one faster. The mean was read until a
/// busy macOS runner measured a scroll step at 21.6 ms against the 12 ms alarm, on a pull request that touched no
/// sheet. The time still catches what the counts cannot: work per node, and work outside the tree. The ceilings
/// follow the harness's rule (see <see cref="PerfHarnessTests"/>): an order of magnitude loose.
/// </para>
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

    /// <summary>The layout nodes a frame walked: every node of the tree it laid out, overlays included.</summary>
    private static int Walked(PhotonHost host) =>
        Walk(host.LastFrame!.Root).Count() + host.LastFrame!.OverlayRoots.Sum(root => Walk(root).Count());

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

        var window = Walked(host);
        const int keys = 40;
        var times = new double[keys];
        var walked = new int[keys];
        for (var i = 0; i < keys; i++)
        {
            var clock = Stopwatch.StartNew();
            // Across and back inside the window: each moves the active cell and builds the sheet again.
            host.KeyDown(i % 2 == 0 ? "ArrowRight" : "ArrowLeft").Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            times[i] = clock.Elapsed.TotalMilliseconds;
            walked[i] = Walked(host);
        }
        var cells = Cells(host).Count;

        _output.WriteLine($"a keystroke at 100,000 × 100: fastest {times.Min():F2} ms, mean {times.Average():F2} ms, {cells} cells in the window, "
            + $"{walked.Max()} layout nodes (alarm {KeystrokeCeilingMs} ms on the fastest)");
        cells.Should().BeLessThan(1_500, "the window and its overscan, never a row or a column whole");
        walked.Max().Should().BeLessThan(window * 2, "a keystroke lays out a screenful, as the frame before it did");
        times.Min().Should().BeLessThan(KeystrokeCeilingMs, "a keystroke builds a screenful");
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

        var window = Walked(host);
        const int keys = 40;
        var times = new double[keys];
        var walked = new int[keys];
        for (var i = 0; i < keys; i++)
        {
            var clock = Stopwatch.StartNew();
            // Each character grows the entry, measures its line and builds the sheet with its editor.
            host.TextInput(i % 2 == 0 ? "a" : "b").Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            times[i] = clock.Elapsed.TotalMilliseconds;
            walked[i] = Walked(host);
        }

        _output.WriteLine($"a character into an entry at 100,000 × 100: fastest {times.Min():F2} ms, mean {times.Average():F2} ms, "
            + $"{sheet.Entry.Document.Text.Length} characters, the editor over {sheet.EditorRange.ColCount} columns, {walked.Max()} layout nodes "
            + $"(alarm {KeystrokeCeilingMs} ms on the fastest)");
        sheet.Editing.Should().BeTrue();
        sheet.Entry.Document.Text.Length.Should().Be(45);
        walked.Max().Should().BeLessThan(window * 2, "a character lays out a screenful and its editor, as the frame before it did");
        times.Min().Should().BeLessThan(KeystrokeCeilingMs, "a character builds a screenful and its editor");
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
        var window = Walked(host);

        const int steps = 40;
        var times = new double[steps];
        var walked = new int[steps];
        for (var i = 0; i < steps; i++)
        {
            var clock = Stopwatch.StartNew();
            // Two points either way: well inside the three rows and columns of overscan.
            host.ScrollBy(at.X, at.Y, i % 2 == 0 ? 2 : -2, i % 2 == 0 ? 2 : -2, stepped: false).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            times[i] = clock.Elapsed.TotalMilliseconds;
            walked[i] = Walked(host);
        }
        var after = Cells(host);

        _output.WriteLine($"a scroll step inside the window at 100,000 × 100: fastest {times.Min():F2} ms, mean {times.Average():F2} ms, "
            + $"{after.Count} cells, {window} layout nodes (alarm {StepCeilingMs} ms on the fastest)");
        after.Should().HaveSameCount(before);
        after.Zip(before).Should().OnlyContain(pair => ReferenceEquals(pair.First, pair.Second),
            "a step inside the built cells moves them and builds none, the sheet included");
        walked.Should().AllBeEquivalentTo(window, "a step inside the window lays out the nodes in view, the same ones");
        times.Min().Should().BeLessThan(StepCeilingMs, "a step inside the window lays out what is in view");
    }
}
