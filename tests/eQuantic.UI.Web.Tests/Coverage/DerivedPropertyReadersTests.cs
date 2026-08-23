using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests.Coverage;

/// <summary>
/// A derived property exists because the raw field is the WRONG answer somewhere. Nothing stops a
/// caller reading the field anyway, and when they do it is silent: the value is a real value, just
/// not the one the node means.
/// <para>
/// It cost four bugs in one day. A Text built from runs carries an empty <c>Content</c> — the words
/// are in <c>Spans</c>, and <c>PlainContent</c> exists for exactly that. Read from the field: a
/// styled paragraph produced no accessibility node at all, a control with a styled label reached
/// the platform with NO NAME, an authored newline inside a run stopped turning the line, and every
/// styled tooltip hashed the same empty string so two of them shared one id.
/// </para>
/// <para>
/// None of it was visible to the parity fixture, because the TypeScript twin read the same wrong
/// field and the two sides agreed. Agreement is not correctness — so the guard is on the SOURCE,
/// which is the only place the mistake is visible at all.
/// </para>
/// </summary>
public class DerivedPropertyReadersTests
{
    /// <summary>Walked ONCE. The scan visits hundreds of files, and re-walking the parents for
    /// each one is filesystem IO to answer a question whose answer cannot change.</summary>
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static IEnumerable<(string Path, string Text)> Sources(params string[] roots)
    {
        foreach (var root in roots)
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, root), "*.cs",
                     SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            yield return (Path.GetRelativePath(Root, file).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllText(file));
        }
    }

    private static string Baseline(string name) => Path.Combine(
        Root, "tests", "eQuantic.UI.Web.Tests", "Coverage", name);

    // A read of a Text's Content FIELD, in the three spellings the tree uses.
    private static readonly Regex RawRead = new(
        @"\btext\s*\.\s*Content\b|\bText\s*\{\s*Content\b|\bText\s+\w+\s+when\s+\w+\.Content\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Who still reads the raw field. This list may only SHRINK — every entry has to be a place
    /// where the field genuinely IS the answer, and the entry says so. A new one is a bug waiting
    /// for the shape of text that makes it visible, which is the shape nobody tests with.
    /// Regenerate with EQ_UPDATE_RAW_READ_BASELINE=1 after REMOVING one.
    /// </summary>
    [Fact]
    public void NoNewReaderOfTheRawContentField()
    {
        var current = Sources("src")
            .SelectMany(source => source.Text.Split('\n')
                .Select((line, index) => (source.Path, Number: index + 1, Line: line.Trim()))
                .Where(entry => RawRead.IsMatch(entry.Line)))
            .Select(entry => $"{entry.Path}  {entry.Line}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        var path = Baseline("raw-content-readers.baseline.txt");
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_RAW_READ_BASELINE") == "1")
        {
            File.WriteAllText(path, string.Join("\n", current) + "\n");
            return;
        }

        var committed = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        current.Except(committed).Should().BeEmpty(
            "a new read of the raw Content field. A Text built from runs has an EMPTY Content — if "
            + "this site is not guarded by a Spans check, it wants PlainContent. If it genuinely is "
            + "guarded, add it to the baseline with the reason.");
    }

    /// <summary>
    /// The other half, and it has the OPPOSITE rule: the set of derived-with-fallback properties may
    /// legitimately GROW, so this fails when one is ADDED rather than when one is removed. A second
    /// property of this shape is the moment somebody has to decide whether its readers are right —
    /// which is much cheaper than deciding it four call sites later.
    /// </summary>
    [Fact]
    public void PlainContentIsStillTheOnlyDerivedPropertyWithARawTwin()
    {
        var property = new Regex(@"public\s+[\w<>?\[\]]+\s+(\w+)\s*=>\s*([^;]{5,200});", RegexOptions.Compiled);
        var member = new Regex(@"^\s*(?:public|internal)\s+(?:readonly\s+)?[\w<>?\[\],\s]+?\s(\w+)\s*(?:\{\s*get|;)",
            RegexOptions.Compiled | RegexOptions.Multiline);
        // The FALLBACK arm only: what the property answers when its condition does not hold. That
        // arm being a bare sibling member is what makes the raw read tempting AND wrong.
        var fallback = new Regex(@"(?::|\?\?)\s*([A-Za-z_]\w*)\s*$", RegexOptions.Compiled);

        var found = new List<string>();
        foreach (var (path, text) in Sources("src/eQuantic.UI.Primitives", "src/eQuantic.UI.Components"))
        {
            var members = member.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            foreach (Match match in property.Matches(text))
            {
                var name = match.Groups[1].Value;
                var body = Regex.Replace(match.Groups[2].Value, @"\s+", " ");
                foreach (Match arm in fallback.Matches(body))
                    if (arm.Groups[1].Value != name && members.Contains(arm.Groups[1].Value))
                        found.Add($"{path}  {name} -> {arm.Groups[1].Value}");
            }
        }

        found.Should().ContainSingle().Which.Should().EndWith("PlainContent -> Content",
            "a second derived property with a raw twin is a decision point, not a detail: every "
            + "reader of the raw field has to be checked before it ships, because getting it wrong "
            + "is silent and only shows for the shape of data nobody writes tests with. Add it here "
            + "deliberately once its readers are verified.");
    }
}
