using System.Diagnostics;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the element engine costs when every member of a keyed list changes, as a table window that
/// moves a page changes all of its cells' (#1505): every key replaced, or every key kept with a member
/// of another type. Each new member asks the old list's table for its key and is made when no old
/// member with that key can be taken, so either costs what building the list and letting the old one
/// go cost, and never a pass over the old members per new one.
/// <para>
/// The ruler is a RATIO, the replacement against the first build of the same list on the same
/// machine a moment before, so a loaded machine moves both sides alike; what it refuses is the
/// quadratic pass, which at this size cost the replacement about a thousand times the build. The
/// ceiling sits an order of magnitude above what the engine measures, as the harness's rule asks.
/// </para>
/// </summary>
[Collection(PerfHarnessCollection.Name)]
public class KeyedListPerfTests
{
    private const int Members = 10_000;
    private const double Ceiling = 20;

    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    private readonly ITestOutputHelper _output;

    public KeyedListPerfTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryMemberReplaced_CostsWhatBuildingTheListCosts(bool keysKept)
    {
        Measure(200, keysKept);
        var (build, replace) = (double.MaxValue, double.MaxValue);
        for (var run = 0; run < 3; run++)
        {
            var (b, r) = Measure(Members, keysKept);
            build = Math.Min(build, b);
            replace = Math.Min(replace, r);
        }

        var ratio = replace / build;
        var what = keysKept ? "every key kept with another type" : "every key replaced";
        _output.WriteLine($"{Members:N0} keyed members: build {build:F1} ms, {what} {replace:F1} ms ({ratio:F1}x, ceiling {Ceiling}x)");
        ratio.Should().BeLessThan(Ceiling,
            "a member no old one with its key can be is made at once, without a pass over the old members");
    }

    private static (double Build, double Replace) Measure(int members, bool keysKept)
    {
        var (first, second) = (Keyed("a", members, row: false), Keyed(keysKept ? "a" : "b", members, row: keysKept));
        var owner = ComponentBoundary.Owner();
        var clock = Stopwatch.StartNew();
        Frame(owner, first);
        var build = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        Frame(owner, second);
        return (build, clock.Elapsed.TotalMilliseconds);
    }

    private static Column Keyed(string prefix, int members, bool row)
    {
        var column = new Column();
        for (var i = 0; i < members; i++)
            column.Add(row ? new Row { Key = prefix + i } : new Text("cell", TypeRole.Caption) { Key = prefix + i });
        return column;
    }

    private static void Frame(BuildOwner owner, VisualNode root)
    {
        owner.Build(root, Context, WindowSizeClass.Compact, rebuildAll: true);
        owner.EndFrame();
    }
}
