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

        var json = JsonSerializer.Serialize(pinned, new JsonSerializerOptions { WriteIndented = true });
        var path = FixturePath();
        var current = File.Exists(path) ? File.ReadAllText(path) : null;
        if (current?.TrimEnd() == json.TrimEnd()) return;

        File.WriteAllText(path, json + Environment.NewLine);
        current.Should().NotBeNull(
            "the fixture did not exist and has now been written — commit it and re-run");
        // Regenerated rather than failed: the values are DERIVED from C#, which is the source. What
        // must not drift is the TWIN's answer to them, and vitest asks that.
    }
}
