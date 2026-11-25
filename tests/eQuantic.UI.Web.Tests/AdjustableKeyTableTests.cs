using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The two halves of an adjustable's keyboard, pinned against each other (#1873). The table both C# targets read is
/// <see cref="AdjustableKeys"/>; the runtime's <c>lowerAdjustable</c> answers the arrows after hydration with its own
/// mapping, which decides by whether the node carries a value where the table decides by role. A key one half steps
/// and the other does not, or steps the other way, is a radio group whose arrows change direction when the page
/// finishes loading, and nothing else in the suite would notice.
/// <para>Regenerate with <c>EQ_UPDATE_ADJUSTABLE_KEYS=1</c>; the runtime's <c>adjustable.spec.ts</c> reads it.</para>
/// </summary>
public class AdjustableKeyTableTests
{
    /// <summary>The four arrows, and keys that must reach the page.</summary>
    private static readonly string[] Probed = ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "Tab", " "];

    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared", "adjustable-keys.fixture.json");
    }

    [Fact]
    public void TheKeyTableIsPinnedForTheClientHalf()
    {
        var table = new SortedDictionary<string, SortedDictionary<string, int?>>(StringComparer.Ordinal);
        foreach (var role in Enum.GetValues<AdjustableRole>())
        {
            // The camelCase spelling is what the enum crosses as, so the twin compares like for like.
            var name = role.ToString();
            var steps = new SortedDictionary<string, int?>(StringComparer.Ordinal);
            foreach (var key in Probed) steps[key] = AdjustableKeys.Step(key, role);
            table[char.ToLowerInvariant(name[0]) + name[1..]] = steps;
        }

        var json = FixtureJson.Write(table);
        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_ADJUSTABLE_KEYS") == "1")
        {
            File.WriteAllText(path, json);
            return;
        }

        File.Exists(path).Should().BeTrue(
            "the client half asserts against this — generate it with EQ_UPDATE_ADJUSTABLE_KEYS=1");
        File.ReadAllText(path).Should().Be(json,
            "the key table changed; regenerate with EQ_UPDATE_ADJUSTABLE_KEYS=1 and change the TS half too");
    }
}
