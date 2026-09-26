using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The numbers PRINTED on the design pages, held to the numbers the pins hold.
///
/// <para>
/// <see cref="HandoffTokenPinTests"/> keeps <c>tokens.json</c> honest, but the pages repeat its
/// values in prose ("Track 52×32", "Sizes 24/32/40/56"), and a sentence is a second copy that no
/// pin reads. So a figure that derives from a token is written with its path,
/// <c>&lt;span data-token="avatar.small"&gt;24&lt;/span&gt;</c>, and this compares the text with the
/// token. A figure the SDK can COUNT is written the same way with <c>data-figure</c>, and counted.
/// </para>
///
/// <para>
/// <see cref="Floor"/> is how many token figures the pages carried when this was written. A
/// re-export that dropped the markers would pass every comparison by leaving nothing to compare, so
/// the count is a ratchet: raise it when figures are marked, and never lower it to pass.
/// </para>
/// </summary>
public class HandoffFigureTests
{
    private const int Floor = 52;

    /// <summary>The counted figures (<c>data-figure</c>) the pages carried when this was written: the
    /// component count on Foundations, and three times on the design system's index (its opening, its
    /// header metric and its catalog summary).</summary>
    private const int CountedFloor = 4;

    private static readonly Regex TokenFigure =
        new("data-token=\"(?<path>[^\"]+)\"[^>]*>(?<text>[^<]*)<", RegexOptions.Compiled);

    private static readonly Regex CountedFigure =
        new("data-figure=\"(?<name>[^\"]+)\"[^>]*>(?<text>[^<]*)<", RegexOptions.Compiled);

    private static readonly Regex FirstNumber = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);

    private static readonly string Root = RepositoryRoot();

    private static readonly JsonElement Tokens = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(Root, "docs", "design", "tokens.json"))).RootElement;

    [Fact]
    public void EveryMarkedFigureMatchesItsToken()
    {
        var offences = new List<string>();
        var found = 0;
        foreach (var (where, match) in Matches(TokenFigure))
        {
            found++;
            var path = match.Groups["path"].Value;
            var text = match.Groups["text"].Value;
            if (!TryNumber(path, out var want))
                offences.Add($"{where}: data-token=\"{path}\" names no number in tokens.json");
            else if (!TryShown(text, out var shown) || Math.Abs(shown - want) > 0.0001f)
                offences.Add($"{where}: the page shows \"{text}\" and {path} is "
                    + want.ToString(CultureInfo.InvariantCulture));
        }

        found.Should().BeGreaterThanOrEqualTo(Floor,
            "the pages carried this many token figures when the pin was written. Fewer means markers were "
            + "dropped by a re-export or a rewrite, and a figure without its marker is a number nothing checks");
        offences.Should().BeEmpty(
            "a figure printed on a page has to be the token it quotes. Correct the page, or the token if the "
            + "page is right:" + List(offences));
    }

    /// <summary>
    /// "How many components ship" is a figure the SDK answers by itself: every public, concrete
    /// <see cref="StatelessComponent"/> or <see cref="StatefulComponent"/> in eQuantic.UI.Components.
    /// </summary>
    [Fact]
    public void CountedFiguresMatchTheSdk()
    {
        var components = typeof(global::eQuantic.UI.Components.ListDetail).Assembly.GetTypes()
            .Count(t => t.IsPublic && !t.IsAbstract
                && (typeof(StatelessComponent).IsAssignableFrom(t) || typeof(StatefulComponent).IsAssignableFrom(t)));

        var counted = new Dictionary<string, int>(StringComparer.Ordinal) { ["components.count"] = components };

        var offences = new List<string>();
        var found = 0;
        foreach (var (where, match) in Matches(CountedFigure))
        {
            found++;
            var name = match.Groups["name"].Value;
            var text = match.Groups["text"].Value;
            if (!counted.TryGetValue(name, out var actual))
                offences.Add($"{where}: data-figure=\"{name}\" is not a figure this test knows how to count");
            // Compared as read, never truncated first: a cast let "56.9" pass as the count 56.
            else if (!TryShown(text, out var shown) || shown != actual)
                offences.Add($"{where}: the page shows \"{text}\" and the SDK has {actual}");
        }

        found.Should().BeGreaterThanOrEqualTo(CountedFloor,
            "the pages carried this many counted figures when the pin was written. Fewer means a marker was "
            + "dropped, and the count it carried is a number nothing checks");
        offences.Should().BeEmpty("a counted figure has to be the count:" + List(offences));
    }

    /// <summary>The pages, not the frames they are presented inside (the same rule
    /// <see cref="HandoffVocabularyTests"/> follows).</summary>
    private static IEnumerable<string> Pages() =>
        Directory.EnumerateFiles(Path.Combine(Root, "docs", "design"), "*.html", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(Root, f).Split(Path.DirectorySeparatorChar).Contains("frames"))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static IEnumerable<(string Where, Match Match)> Matches(Regex pattern)
    {
        foreach (var file in Pages())
        {
            var lines = File.ReadAllLines(file);
            var relative = Path.GetRelativePath(Root, file);
            for (var i = 0; i < lines.Length; i++)
                foreach (Match match in pattern.Matches(lines[i]))
                    yield return ($"{relative}:{i + 1}", match);
        }
    }

    private static bool TryNumber(string path, out float value)
    {
        value = 0;
        var node = Tokens;
        foreach (var part in path.Split('.'))
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out var next)) return false;
            node = next;
        }
        if (node.ValueKind != JsonValueKind.Number) return false;
        value = node.GetSingle();
        return true;
    }

    private static bool TryShown(string text, out float value)
    {
        value = 0;
        var match = FirstNumber.Match(text);
        if (!match.Success) return false;
        value = float.Parse(match.Value, CultureInfo.InvariantCulture);
        return true;
    }

    private static string List(IEnumerable<string> items) =>
        Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", items) + Environment.NewLine;

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the pin reads docs/design, so it has to find the tree");
        return dir!.FullName;
    }
}
