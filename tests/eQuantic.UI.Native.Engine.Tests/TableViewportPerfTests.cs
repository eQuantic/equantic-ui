using System.Diagnostics;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a table costs on Photon per scroll, over Excel's whole sheet: a step inside the cells already
/// built, which builds nothing, and a step that leaves them, which builds the window again.
/// <para>
/// The guard is the WORK a step does, counted, which no runner's load can move (#1397): the cells it
/// builds, and the layout nodes its frame walks. A step inside the window builds none and walks the
/// same nodes as the frame before it; a step that leaves the window builds the window and its
/// overscan, never a row or a column whole.
/// </para>
/// <para>
/// Beside it, a loose time alarm, read on the FASTEST step rather than the mean: a shared runner
/// slows some steps and never makes one faster, so the fastest of forty is what the step costs when
/// nothing else is running. The mean was read until a busy macOS runner averaged 22.9 ms against
/// the 12 ms alarm on a pull request that touched no table. The time still catches what the counts
/// cannot: work per node (a step that measures every visible cell's text again) and work outside the
/// tree (a walk over a million rows' spans to find the window). The ceilings follow the harness's
/// rule (see <see cref="PerfHarnessTests"/>): an order of magnitude loose.
/// </para>
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

    /// <summary>The layout nodes a frame walked: every node of the tree it laid out, overlays included.</summary>
    private static int Walked(PhotonHost host)
    {
        var frame = host.LastFrame!;
        return Count(frame.Root) + frame.OverlayRoots.Sum(Count);

        static int Count(LayoutNode node)
        {
            var count = 1;
            foreach (var child in node.Children) count += Count(child);
            return count;
        }
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
        var window = Walked(host);
        const int steps = 40;
        var times = new double[steps];
        var walked = new int[steps];
        for (var i = 0; i < steps; i++)
        {
            var clock = Stopwatch.StartNew();
            // Two points either way: well inside the three rows of overscan.
            host.ScrollBy(x, y, i % 2 == 0 ? 2 : -2, i % 2 == 0 ? 2 : -2, stepped: false).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            times[i] = clock.Elapsed.TotalMilliseconds;
            walked[i] = Walked(host);
        }

        _output.WriteLine($"a scroll step inside the window at 1,048,576 × 16,384: fastest {times.Min():F2} ms, "
            + $"mean {times.Average():F2} ms (alarm {StepCeilingMs} ms on the fastest), {window} layout nodes");
        counter.Built.Should().Be(built, "a step inside the built cells moves them and builds none");
        walked.Should().AllBeEquivalentTo(window, "a step inside the window lays out the nodes in view, the same ones");
        times.Min().Should().BeLessThan(StepCeilingMs, "a step inside the window lays out what is in view");
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
        var window = Walked(host);
        const int steps = 20;
        var times = new double[steps];
        var walked = new int[steps];
        for (var i = 0; i < steps; i++)
        {
            var clock = Stopwatch.StartNew();
            // A page down and across: every visible row and column leaves the cells built.
            host.ScrollBy(x, y, 1500, 900, stepped: false).Should().BeTrue();
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            host.RenderFrame(new DisplayListBuilder(), time += 16);
            times[i] = clock.Elapsed.TotalMilliseconds;
            walked[i] = Walked(host);
        }
        var cellsEach = (counter.Built - built) / (double)steps;

        _output.WriteLine($"a window rebuild at 1,048,576 × 16,384: fastest {times.Min():F2} ms, mean {times.Average():F2} ms, "
            + $"{cellsEach:F0} cells built, {walked.Max()} layout nodes (alarm {RebuildCeilingMs} ms on the fastest)");
        cellsEach.Should().BeLessThan(1500, "a rebuild builds the window and its overscan, never a row or a column whole");
        walked.Max().Should().BeLessThan(window * 2, "a rebuilt window lays out a screenful, as the one it replaced did");
        times.Min().Should().BeLessThan(RebuildCeilingMs, "a rebuild costs a screenful");
    }
}
