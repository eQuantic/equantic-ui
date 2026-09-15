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

    /// <summary>The same, widened to double — see the fractional block below for why.</summary>
    private static object WideCorners(Rect rect) =>
        new { x = (double)rect.X, y = (double)rect.Y, width = (double)rect.Width, height = (double)rect.Height };

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
            // POINT's two arithmetic members, which are the ones a `Rect` case cannot reach: a
            // Rect pin exercises `Center`, and `Dot`/`Length` are never on that path. Both round
            // at every step in the subject — `Dot` is two float multiplies and a float add,
            // `Length` a float sqrt over them — so a twin that drops one `fround` answers a
            // different number here and nowhere else.
            //
            // `Length` is the discriminating one to read: a 3-4-5 triangle scaled by a tenth is
            // EXACTLY 0.5 in floats and 0.500000011920929 in doubles. Widened to double on the way
            // out for the same reason the fractional Rect values are — .NET prints a float as its
            // shortest round-tripping spelling, and the browser has only doubles to print.
            point = new
            {
                dot = (double)new Point(0.1f, 0.2f).Dot(new Point(0.3f, 0.4f)),
                length = (double)new Point(0.3f, 0.4f).Length(),
                lengthFractional = (double)new Point(0.1f, 0.2f).Length(),
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
                // FRACTIONAL, because the twin does this arithmetic in doubles unless it is told
                // not to. Every component here is a C# `float`, so `Right` is a float ADD and the
                // last bit differs from the double the browser would compute — which is enough to
                // classify a pointer on an edge differently. 0.1f and 0.2f are the classic pair
                // whose sum is not what it looks like in either precision.
                //
                // WIDENED TO DOUBLE on the way out, and that is the half that took a failing test
                // to see: .NET serializes a float as the shortest string that round-trips AS A
                // FLOAT, so `0.1f + 0.3f` prints "0.4" while the number is 0.4000000059604645 — and
                // JavaScript, which has only doubles, prints the second. Comparing the two would
                // fail on a twin that is exactly right. The fixture carries the VALUE.
                fractionalRight = (double)new Rect(0.1f, 0.2f, 0.3f, 0.4f).Right,
                fractionalBottom = (double)new Rect(0.1f, 0.2f, 0.3f, 0.4f).Bottom,
                fractionalCenter = new
                {
                    x = (double)new Rect(0.1f, 0.2f, 0.3f, 0.4f).Center.X,
                    y = (double)new Rect(0.1f, 0.2f, 0.3f, 0.4f).Center.Y,
                },
                fractionalInflated = WideCorners(new Rect(0.1f, 0.2f, 0.3f, 0.4f).Inflate(0.05f)),
                fractionalInflatedRight = (double)new Rect(0.1f, 0.2f, 0.3f, 0.4f).Inflate(0.05f).Right,
                // PARAMETERS, which is the third place this rule lands and the one a twin forgets:
                // these arguments are single precision BEFORE `right - left` runs, because C# does
                // the conversion at the call. A twin that takes doubles and rounds only the result
                // answers a different width. Discriminating on purpose — with 0.1 and 0.3 the two
                // orders differ in the last bit.
                fractionalFromLTRB = WideCorners(Rect.FromLTRB(0.1f, 0.2f, 0.3f, 0.7f)),
                // The readable one: inflating by exactly the x it sits at lands on ZERO in floats,
                // and on 1.49e-09 if the amount was never rounded.
                fractionalInflatedByItsOwnX = WideCorners(new Rect(0.1f, 0.2f, 0.3f, 0.4f).Inflate(0.1f)),
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

        var json = FixtureJson.Write(pinned);
        var path = FixturePath();

        // Behind the env var, like every other fixture here — and unlike what this test used to
        // do, which was to REWRITE the file on any ordinary run and pass. The values are derived
        // from C#, so regenerating them is cheap and that made it look harmless; it is not. Vitest
        // reads this same file in another process, so a changed primitive updated the QUESTION
        // before the twin was ever asked the old one, and the diff nobody had to look at is the
        // one that would have said so. `PrimitivesRuntimeExportTests` beside it already asked for
        // the variable.
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_PRIMITIVE_VALUES") == "1")
        {
            File.WriteAllText(path, json);
            return;
        }

        File.Exists(path).Should().BeTrue(
            "the twin asserts against this fixture — write it with EQ_UPDATE_PRIMITIVE_VALUES=1 "
            + "and commit it");
        File.ReadAllText(path).Should().Be(json,
            "a primitive's value changed — regenerate with EQ_UPDATE_PRIMITIVE_VALUES=1 and commit "
            + "the fixture WITH the change that caused it, so the twin is asked the new question");
    }
}
