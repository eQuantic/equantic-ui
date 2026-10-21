using System.Diagnostics;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a table costs on Photon per scroll, over Excel's whole sheet: a step inside the cells already
/// built, which builds nothing, and a step that leaves them, which builds the window again. The
/// ceilings follow the harness's rule (see <see cref="PerfHarnessTests"/>): an order of magnitude
/// loose, because CI machines are shared, and still far below what a table that built its rows, or
/// laid out what it does not show, would cost.
/// </summary>
[Collection(PerfHarnessCollection.Name)]
public class TableViewportPerfTests
{
    private const double StepCeilingMs = 12;
    private const double RebuildCeilingMs = 100;

    private readonly ITestOutputHelper _output;

    public TableViewportPerfTests(ITestOutputHelper output) => _output = output;

    private sealed class Counter
    {
        public int Built;
    }

    private static (PhotonHost Host, float X, float Y) Open(Counter counter)
    {
        var table = new TableView(new UniformTableSpans(1_048_576 + 1, 28), new UniformTableSpans(16_384 + 1, 96),
            (row, column) =>
            {
                counter.Built++;
                return new Text($"{row}:{column}", TypeRole.Caption, maxLines: 1);
            })
        {
            PinnedRows = 1,
            PinnedColumns = 1,
        };
        var host = new PhotonHost(table, PhotonTheme.Instance, ThemeMode.Light, 1440, 900)
        {
            SmoothScroll = false,
            ReducedMotion = true,
        };
        for (var t = 0; t < 4; t++) host.RenderFrame(new DisplayListBuilder(), t * 16);
        var region = host.LastFrame!.TableRegions.Single();
        return (host, region.Viewport.Scroll.Viewport.Width / 2 + 100, 400);
    }

    [Fact]
    public void AStepInsideTheBuiltCells_BuildsNothing_AndStaysInsideTheAlarm()
    {
        var counter = new Counter();
        var (host, x, y) = Open(counter);
        var time = 100f;
        // Warm, and back where the timed steps start.
        for (var i = 0; i < 5; i++)
        {
            host.ScrollBy(x, y, 0, 4, stepped: false);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            host.ScrollBy(x, y, 0, -4, stepped: false);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }

        var built = counter.Built;
        const int steps = 40;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < steps; i++)
        {
            // Two points either way: well inside the three rows of overscan.
            host.ScrollBy(x, y, i % 2 == 0 ? 2 : -2, i % 2 == 0 ? 2 : -2, stepped: false).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }
        var each = clock.Elapsed.TotalMilliseconds / steps;

        _output.WriteLine($"a scroll step inside the window at 1,048,576 × 16,384: {each:F2} ms (alarm {StepCeilingMs} ms)");
        counter.Built.Should().Be(built, "a step inside the built cells moves them and builds none");
        each.Should().BeLessThan(StepCeilingMs, "a step inside the window lays out what is in view");
    }

    [Fact]
    public void AStepThatLeavesTheBuiltCells_RebuildsOnlyTheWindow_AndStaysInsideTheAlarm()
    {
        var counter = new Counter();
        var (host, x, y) = Open(counter);
        var time = 100f;
        for (var i = 0; i < 3; i++)
        {
            host.ScrollBy(x, y, 0, 900, stepped: false);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }

        var built = counter.Built;
        const int steps = 20;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < steps; i++)
        {
            // A page down and across: every visible row and column leaves the cells built.
            host.ScrollBy(x, y, 1500, 900, stepped: false).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
        }
        var each = clock.Elapsed.TotalMilliseconds / steps;
        var cellsEach = (counter.Built - built) / (double)steps;

        _output.WriteLine($"a window rebuild at 1,048,576 × 16,384: {each:F2} ms, {cellsEach:F0} cells built "
            + $"(alarm {RebuildCeilingMs} ms)");
        cellsEach.Should().BeLessThan(1500, "a rebuild builds the window and its overscan, never a row or a column whole");
        each.Should().BeLessThan(RebuildCeilingMs, "a rebuild costs a screenful");
    }
}
