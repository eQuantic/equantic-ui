using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every "Since 0.2.0-preview.N" in the wiki names the release the feature ACTUALLY shipped in.
/// <para>
/// The marks exist so a reader can tell whether the version they are on has the thing. Nothing
/// checked them, and one was wrong: `InFlow` was documented as preview.15 and shipped in .16 — and
/// since the site mirrors the wiki, a wrong mark is a wrong answer published to everyone reading
/// the documentation.
/// </para>
/// <para>
/// A mark carries the symbol it claims, as an HTML comment the renderer drops:
/// <c>*Since **0.2.0-preview.16*** &lt;!-- since: class InFlow --&gt;</c>. Prose cannot be checked;
/// a search string can. This finds the commit that introduced it and the earliest tag containing
/// that commit, which is the release it went out in.
/// </para>
/// </summary>
public class WikiVersionMarkTests
{
    private static string? RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here?.FullName;
    }

    private static string Git(string root, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    /// <summary>
    /// The release a symbol went out in: the FIRST commit whose diff contains it, then the earliest
    /// tag reachable from there. Null when it has not shipped yet — a mark on unreleased work is a
    /// promise, and this test is about what is true.
    /// </summary>
    /// <summary>
    /// A version mark, in either spelling the wiki has used.
    /// <para>
    /// The STRUCTURED one names its parts (<c>eq:since 0.2.0-preview.23 symbol="X" src="path"</c>);
    /// the older one puts the symbol and its path in one string (<c>since: X @ path</c>). Both are
    /// read, because the spelling migrated once while this guard was watching and it went on passing
    /// — having matched nothing at all.
    /// </para>
    /// </summary>
    private const string MarkPattern =
        @"(?m)^\*Since \*\*(?<version>[^*]+)\*\*\*\s*<!--\s*(?:"
        + @"since:\s*(?<symbol>[^>]+?)"
        + @"|eq:since\s+\S+\s+symbol=""(?<name>[^""]+)""(?:\s+src=""(?<src>[^""]+)"")?"
        + @")\s*-->";

    private static string? ShippedIn(string root, string search)
    {
        // `text @ path` narrows the search — a member name is rarely unique across a framework that
        // has both a Core and a Primitives component model, and `OnMount` matched the older one.
        var path = "";
        if (search.Split(" @ ") is [var text, var scope])
        {
            search = text;
            path = $" -- {scope}";
        }

        var commits = Git(root, $"log --all --reverse --format=%H -S\"{search}\"{path}")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (commits.Length == 0) return null;

        var tags = Git(root, $"tag --contains {commits[0]}")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(tag => tag.StartsWith("v0.", StringComparison.Ordinal))
            .Select(tag => (Tag: tag, Order: PreviewOrder(tag)))
            .Where(entry => entry.Order >= 0)
            .OrderBy(entry => entry.Order)
            .ToList();

        return tags.Count == 0 ? null : tags[0].Tag[1..];
    }

    /// <summary>Sorts preview.9 BEFORE preview.10 — the whole point of the check is off-by-one
    /// version numbers, and a lexical sort has its own.</summary>
    private static int PreviewOrder(string tag) =>
        Regex.Match(tag, @"preview\.(\d+)$") is { Success: true } match
            ? int.Parse(match.Groups[1].Value)
            : -1;

    [Fact]
    public void EveryVersionMark_NamesTheReleaseItActuallyShippedIn()
    {
        var root = RepoRoot();
        root.Should().NotBeNull();

        var wiki = Path.Combine(Directory.GetParent(root!)!.FullName, "equantic-ui.wiki");
        // The wiki is a SEPARATE repository, so this guard bites where the wiki is checked out —
        // which is where the docs are written. It does not run in CI unless CI clones the wiki
        // beside the repo; saying so here beats a green tick that checked nothing.
        if (!Directory.Exists(wiki)) return;

        // Without tags every symbol reads as unreleased and the check passes having compared
        // nothing — the exact shape of green tick this guard exists to replace. A shallow CI
        // checkout has none by default.
        Git(root!, "tag --list v0.2.0-preview.*").Should().NotBeEmpty(
            "the guard compares against tags; fetch them before running it");

        var wrong = new List<string>();
        var checkedMarks = 0;
        foreach (var page in Directory.GetFiles(wiki, "*.md"))
        {
            var text = File.ReadAllText(page);
            foreach (Match mark in Regex.Matches(text, MarkPattern))
            {
                var claimed = mark.Groups["version"].Value.Trim();
                // The structured spelling names the symbol in an attribute and may add the path in
                // another; the older one puts both in one string as `symbol @ path`.
                var symbol = mark.Groups["symbol"].Success
                    ? mark.Groups["symbol"].Value
                    : mark.Groups["name"].Value
                      + (mark.Groups["src"].Success ? $" @ {mark.Groups["src"].Value}" : "");
                checkedMarks++;
                // `? reason` — a mark about work that predates the tags, or that no single symbol
                // pins. Rare and VISIBLE, like every other exception list here: it says "unchecked"
                // rather than guessing a symbol that would make the test agree with itself.
                if (symbol.StartsWith('?')) continue;
                var actual = ShippedIn(root!, symbol);
                if (actual is not null && actual != claimed)
                    wrong.Add($"{Path.GetFileName(page)}: `{symbol}` says {claimed}, shipped in {actual}");
            }
        }

        // A guard that parses NOTHING passes, and that is the failure this whole file exists to
        // prevent — pointed at itself. The mark spelling changed once under exactly these feet;
        // counting what was compared is what turns that from a green tick into a red one.
        checkedMarks.Should().BeGreaterThan(0,
            "the wiki has version marks; parsing none means the spelling moved and this guard did not");

        // Every one of them, not the first: finding these one round trip at a time is how a docs
        // audit turns into an afternoon.
        string.Join("\n", wrong).Should().BeEmpty(
            "a version mark is the reader's answer to \"does my version have this\", and the site "
            + "publishes it");
    }

    /// <summary>
    /// A mark with no symbol cannot be checked, so it must not exist. Without this the guard is
    /// opt-in, and the next mark someone writes opts out by accident.
    /// </summary>
    [Fact]
    public void EveryVersionMark_SaysWhatItIsAbout()
    {
        var root = RepoRoot();
        var wiki = Path.Combine(Directory.GetParent(root!)!.FullName, "equantic-ui.wiki");
        if (!Directory.Exists(wiki)) return;   // see above

        var unannotated = new List<string>();
        foreach (var page in Directory.GetFiles(wiki, "*.md"))
        {
            var text = File.ReadAllText(page);
            // At the START of a line: Home.md explains the convention in prose, and an example of
            // a mark is not one.
            foreach (Match mark in Regex.Matches(text, @"(?m)^\*Since \*\*[^*]+\*\*\*(?<tail>.{0,40})"))
            {
                var tail = mark.Groups["tail"].Value;
                // Either spelling — see MarkPattern. Both name a symbol; only the punctuation moved.
                if (!tail.Contains("<!-- since:", StringComparison.Ordinal)
                    && !tail.Contains("<!-- eq:since", StringComparison.Ordinal))
                    unannotated.Add($"{Path.GetFileName(page)}: {mark.Value.Split('\n')[0]}");
            }
        }

        string.Join("\n", unannotated).Should().BeEmpty(
            "a mark carries the symbol it claims — `<!-- eq:since … symbol=\"Foo\" -->` — or "
            + "nothing can check it");
    }
}
