using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every custom <c>[Fact]</c> in the test TREE is applied to at least one test.
/// <para>
/// These attributes exist to SKIP — <c>MacFact</c>, <c>WindowsFact</c>, <c>MacFontFact</c>, and the
/// <c>CultureDataFact</c> this guard was written after — so an unused one is not dead code the way
/// an unused helper is. It is a fence standing in an empty field: a reader greps the name, finds a
/// class whose doc explains a real constraint, and concludes the constraint is enforced. Nothing
/// says otherwise, because a skip attribute nobody applies breaks nothing.
/// </para>
/// <para>
/// Measured, and it is why this exists: <c>CultureDataFact</c> fenced two pins whose subject was the
/// host's ICU tables. A later change replaced those pins with ones that assert the MAPPING we own
/// rather than the tables we do not — the better answer — and the attribute stayed behind, declared,
/// documented, and applied to nothing. Nobody was wrong; the suite simply stopped saying something
/// it still looked like it said.
/// </para>
/// <para>
/// Scanned from SOURCE across the whole tree rather than by reflection over this assembly, and that
/// is the point: reflection sees only its own project, and this project has no custom Fact left at
/// all — so a reflection guard here would pass over an empty set, which is the shape of instrument
/// this one exists to catch. Read from the tree, it runs against the three that are really there.
/// </para>
/// </summary>
public class AFenceNobodyAppliesTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        sourcePath;

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                // This file SPELLS the declaration it looks for, in the comment explaining it.
                && Path.GetFileName(path) != Path.GetFileName(ThisFile()));

    [Fact]
    public void EveryCustomFactInTheTree_IsAppliedSomewhere()
    {
        // `class FooFactAttribute : FactAttribute` / `: TheoryAttribute`, however it is spaced.
        var declaration = new Regex(
            @"class\s+(?<name>\w+?)Attribute\s*:\s*(Fact|Theory)Attribute", RegexOptions.Compiled);

        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = new List<string>();
        foreach (var path in Sources())
        {
            var source = File.ReadAllText(path);
            text.Add(source);
            foreach (Match match in declaration.Matches(source))
                declared[match.Groups["name"].Value] = Path.GetRelativePath(RepoRoot(), path);
        }

        declared.Should().NotBeEmpty(
            "the tree has custom Facts (MacFact, WindowsFact, MacFontFact) — finding none means this "
            + "guard stopped matching the declaration and is now passing over an empty set");

        var unapplied = declared
            .Where(entry => !text.Any(source => source.Contains($"[{entry.Key}]", StringComparison.Ordinal)
                || source.Contains($"[{entry.Key}(", StringComparison.Ordinal)))
            .Select(entry => $"{entry.Key} ({entry.Value})")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        unapplied.Should().BeEmpty(
            "a custom Fact exists to skip something, so one nobody applies is a fence standing in an "
            + "empty field — it reads as an enforced constraint and enforces nothing. Apply it, or "
            + "delete it with the pins it was fencing");
    }
}
