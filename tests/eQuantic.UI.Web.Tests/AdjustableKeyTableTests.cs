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
/// <para>The fixture holds the table at each direction (<c>leftToRight</c>, <c>rightToLeft</c>), because the horizontal
/// arrows swap where the end of the row is on the left (#1569): the twin reads the direction from the nearest
/// <c>[dir]</c> at the key, and the spec presses each section's keys inside an element of that direction.</para>
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

    /// <summary>The camelCase spelling is what an enum crosses as, so the twin compares like for like.</summary>
    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    [Fact]
    public void TheKeyTableIsPinnedForTheClientHalf()
    {
        var tables = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, int?>>>(StringComparer.Ordinal);
        foreach (var direction in Enum.GetValues<TextDirection>())
        {
            var table = new SortedDictionary<string, SortedDictionary<string, int?>>(StringComparer.Ordinal);
            foreach (var role in Enum.GetValues<AdjustableRole>())
            {
                var steps = new SortedDictionary<string, int?>(StringComparer.Ordinal);
                foreach (var key in Probed) steps[key] = AdjustableKeys.Step(key, role, direction);
                table[Camel(role.ToString())] = steps;
            }
            tables[Camel(direction.ToString())] = table;
        }

        var json = FixtureJson.Write(tables);
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

    [Fact]
    public void TheHorizontalArrowsSwapUnderRightToLeft_AndTheVerticalOnesDoNot()
    {
        foreach (var role in Enum.GetValues<AdjustableRole>())
        {
            AdjustableKeys.Step("ArrowLeft", role, TextDirection.RightToLeft).Should().Be(1, $"{role}: the end of the row is on the left");
            AdjustableKeys.Step("ArrowRight", role, TextDirection.RightToLeft).Should().Be(-1, $"{role}: the start of the row is on the right");
            foreach (var key in new[] { "ArrowUp", "ArrowDown" })
                AdjustableKeys.Step(key, role, TextDirection.RightToLeft)
                    .Should().Be(AdjustableKeys.Step(key, role, TextDirection.LeftToRight), $"{role}: {key} does not read the direction");
        }
    }
}
