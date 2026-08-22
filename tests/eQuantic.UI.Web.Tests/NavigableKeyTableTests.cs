using System.Text.Json;
using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The two halves of the grid keyboard, pinned against each other. The abstract layer names MOVES
/// and never keys, so the key table is a REALIZER's — and there are two realizers for one target:
/// this one runs before hydration, `lowering.ts`'s runs after. A key one half claims and the other
/// ignores is a calendar whose arrows work until the page finishes loading, or start working only
/// then, and nothing else in the suite would notice.
/// <para>Regenerate with <c>EQ_UPDATE_NAVIGABLE_KEYS=1</c>.</para>
/// </summary>
public class NavigableKeyTableTests
{
    /// <summary>Every key the design system's C15 keyboard names, plus the ones that must NOT be
    /// claimed — Tab has to leave the composite and ⌘K has to reach the page.</summary>
    private static readonly (string Key, bool Shift)[] Probed =
    [
        ("ArrowLeft", false), ("ArrowRight", false), ("ArrowUp", false), ("ArrowDown", false),
        ("PageUp", false), ("PageDown", false), ("PageUp", true), ("PageDown", true),
        ("Home", false), ("End", false),
        ("Tab", false), ("Enter", false), ("Escape", false), ("k", false), (" ", false),
    ];

    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared",
            "navigable-keys.fixture.json");
    }

    [Fact]
    public void TheKeyTableIsPinnedForTheClientHalf()
    {
        var table = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, shift) in Probed)
        {
            // The camelCase spelling is what the enum crosses as, so the twin compares like for like.
            var move = NavigableKeys.Move(key, shift);
            table[shift ? $"shift+{key}" : key] =
                move is null ? null : char.ToLowerInvariant(move.Value.ToString()[0]) + move.Value.ToString()[1..];
        }

        var json = JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true }) + "\n";
        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_NAVIGABLE_KEYS") == "1")
        {
            File.WriteAllText(path, json);
            return;
        }

        File.Exists(path).Should().BeTrue(
            "the client half asserts against this — generate it with EQ_UPDATE_NAVIGABLE_KEYS=1");
        File.ReadAllText(path).Should().Be(json,
            "the key table changed; regenerate with EQ_UPDATE_NAVIGABLE_KEYS=1 and change the TS half too");
    }

    [Fact]
    public void KeysTheGridDoesNotClaim_ReachThePage()
    {
        // Tab must leave the composite, Escape must reach the dismiss binding, ⌘K the palette.
        NavigableKeys.Move("Tab", false).Should().BeNull();
        NavigableKeys.Move("Escape", false).Should().BeNull();
        NavigableKeys.Move("k", false).Should().BeNull();
        // …and Enter is the CELL's, not the grid's: a gridcell is a button and answers it itself.
        NavigableKeys.Move("Enter", false).Should().BeNull();
    }
}
