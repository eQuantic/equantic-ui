using System.Diagnostics;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the element engine costs when every key of a list changes, as a table window that moves a
/// page changes all of its cells' (#1505). Each new member asks the old list's table for its key and
/// is made when the table does not hold it, so replacing every key of a list costs what building the
/// list and letting the old one go cost, and never a pass over the old members per new one.
/// <para>
/// The ruler is a RATIO, the replacement against the first build of the same list on the same
/// machine a moment before, so a loaded machine moves both sides alike; what it refuses is the
/// quadratic pass, which at this size cost the replacement many times the build.
/// </para>
/// </summary>
[Collection(PerfHarnessCollection.Name)]
public class KeyedListPerfTests
{
    private const int Members = 10_000;
    private const double Ceiling = 5;

    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    private readonly ITestOutputHelper _output;

    public KeyedListPerfTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EveryKeyReplaced_CostsWhatBuildingTheListCosts()
    {
        Measure(200);
        var (build, replace) = (double.MaxValue, double.MaxValue);
        for (var run = 0; run < 3; run++)
        {
            var (b, r) = Measure(Members);
            build = Math.Min(build, b);
            replace = Math.Min(replace, r);
        }

        var ratio = replace / build;
        _output.WriteLine($"{Members:N0} keyed members: build {build:F1} ms, every key replaced {replace:F1} ms ({ratio:F1}x, ceiling {Ceiling}x)");
        ratio.Should().BeLessThan(Ceiling,
            "a key the old list does not hold is made at once, without a pass over the old members");
    }

    private static (double Build, double Replace) Measure(int members)
    {
        var (first, second) = (Keyed("a", members), Keyed("b", members));
        var owner = ComponentBoundary.Owner();
        var clock = Stopwatch.StartNew();
        Frame(owner, first);
        var build = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        Frame(owner, second);
        return (build, clock.Elapsed.TotalMilliseconds);
    }

    private static Column Keyed(string prefix, int members)
    {
        var column = new Column();
        for (var i = 0; i < members; i++) column.Add(new Text("cell", TypeRole.Caption) { Key = prefix + i });
        return column;
    }

    private static void Frame(BuildOwner owner, VisualNode root)
    {
        owner.Build(root, Context, WindowSizeClass.Compact, rebuildAll: true);
        owner.EndFrame();
    }
}
