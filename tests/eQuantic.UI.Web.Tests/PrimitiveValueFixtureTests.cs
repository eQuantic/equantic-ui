using System.Text.Json;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The VALUES the Primitives value types carry, pinned for the TypeScript twins to check against.
/// <para>
/// A twin is a mirror, and nothing compares a mirror to its subject: `SpringSpec.Default` could
/// change here and the client would keep animating with the old numbers, silently, on exactly the
/// half of the framework nobody runs in a debugger. The type-level parity spec proves the export
/// EXISTS; this proves it says the same thing.
/// </para>
/// </summary>
public class PrimitiveValueFixtureTests
{
    /// <summary>A box as its four numbers, so a mismatch says WHICH edge moved.</summary>
    private static object Corners(Rect rect) =>
        new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height };

    private static string FixturePath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;

        here.Should().NotBeNull("the test has to find the repository root to write the fixture");
        return Path.Combine(here!.FullName, "src", "eQuantic.UI.Runtime", "src", "shared",
            "primitive-values.fixture.json");
    }

    [Fact]
    public void TheCarriedValues_ArePinnedForTheTwins()
    {
        var pinned = new
        {
            springDefault = new
            {
                stiffness = SpringSpec.Default.Stiffness,
                damping = SpringSpec.Default.Damping,
                mass = SpringSpec.Default.Mass,
            },
            networkOffline = new
            {
                online = NetworkState.Offline.Online,
                // The enum crosses as its wire string, so that is what the twin has to hold.
                kind = NetworkState.Offline.Kind.ToString().ToLowerInvariant(),
            },
            // GEOMETRY, which is the twin that can drift while still loading. `Point`, `Size` and
            // `Rect` went into the vocabulary when geometry moved down, so they owe an export — and
            // an exported class with arithmetic in it is a second implementation. These are the
            // answers a page would get here, so the answers the browser gives have to match.
            //
            // Chosen for the branches an obvious twin gets wrong: an intersection that MISSES
            // returns an empty box pinned at the would-be corner rather than at the origin, and
            // containment is HALF-OPEN — the left and top edges are inside, the right and bottom
            // are not, which is what keeps two adjacent boxes from both claiming the same pixel.
            rect = new
            {
                overlap = Corners(new Rect(0, 0, 10, 10).Intersect(new Rect(4, 6, 20, 20))),
                disjoint = Corners(new Rect(0, 0, 10, 10).Intersect(new Rect(40, 60, 5, 5))),
                inset = Corners(new Rect(0, 0, 10, 10).Inflate(-2)),
                grown = Corners(new Rect(3, 4, 10, 10).Inflate(2.5f)),
                fromEdges = Corners(Rect.FromLTRB(2, 3, 9, 11)),
                center = new { x = new Rect(3, 4, 10, 20).Center.X, y = new Rect(3, 4, 10, 20).Center.Y },
                containsTopLeft = new Rect(0, 0, 10, 10).Contains(new Point(0, 0)),
                containsBottomRight = new Rect(0, 0, 10, 10).Contains(new Point(10, 10)),
                containsInside = new Rect(0, 0, 10, 10).Contains(new Point(9.99f, 9.99f)),
                emptyOnZeroWidth = new Rect(0, 0, 0, 10).IsEmpty,
            },
            windowSizeClasses = new
            {
                mediumMinDp = WindowSizeClasses.MediumMinDp,
                expandedMinDp = WindowSizeClasses.ExpandedMinDp,
                // The boundaries themselves, where an off-by-one would live.
                atCompact = WindowSizeClasses.FromWidth(599).ToString().ToLowerInvariant(),
                atMedium = WindowSizeClasses.FromWidth(600).ToString().ToLowerInvariant(),
                belowExpanded = WindowSizeClasses.FromWidth(839).ToString().ToLowerInvariant(),
                atExpanded = WindowSizeClasses.FromWidth(840).ToString().ToLowerInvariant(),
            },
        };

        var json = FixtureJson.Write(pinned).TrimEnd('\n');
        var path = FixturePath();
        var current = File.Exists(path) ? File.ReadAllText(path) : null;
        if (current?.TrimEnd() == json.TrimEnd()) return;

        // LF, not the host's: this file is COMMITTED and compared byte for byte, so a
            // Windows regeneration would rewrite every line ending and fail the pin
            // on the next host that read it.
            File.WriteAllText(path, json + "\n");
        current.Should().NotBeNull(
            "the fixture did not exist and has now been written — commit it and re-run");
        // Regenerated rather than failed: the values are DERIVED from C#, which is the source. What
        // must not drift is the TWIN's answer to them, and vitest asks that.
    }
}
