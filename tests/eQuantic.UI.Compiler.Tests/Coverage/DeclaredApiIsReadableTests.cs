using System.Text.RegularExpressions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// Every line of a <c>PublicAPI.*.txt</c> is the analyzer's OWN text for a symbol, character for
/// character, because that is what it compares against. A line that merely looks right fails the
/// build twice over — RS0016 for the signature it cannot find declared, RS0017 for the one it
/// finds declared and cannot match — and says nothing about why the two strings differ.
/// <para>
/// This exists because two shells can only be declared THROUGH a build log. <c>Shell.iOS</c> and
/// <c>Shell.Android</c> compile on no machine without the iOS workload and the Android SDK, so
/// their entries are transcribed out of CI rather than written by <c>scripts/public-api.sh</c>,
/// and a transcription is where an escape survives. One did: a log delivered as JSON carried
/// <c>Func&lt;T&gt;</c> with both brackets escaped, the transcription decoded the closing one, and
/// <c>PhotonApp.Run</c> shipped six literal characters — a backslash, a u, and 003c — where a
/// single <c>&lt;</c> belonged.
/// The analyzer caught it — on macOS, twenty minutes later, in the one job that can build that
/// shell. This catches the same thing in a second, on any machine.
/// </para>
/// </summary>
public class DeclaredApiIsReadableTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static IEnumerable<(string File, int Number, string Text)> DeclaredEntries()
    {
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "PublicAPI.*.txt", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var text = lines[i];
                if (text.Length == 0 || text.StartsWith('#'))
                    continue;
                yield return (Path.GetRelativePath(src, file), i + 1, text);
            }
        }
    }

    /// <summary>
    /// No entry carries an escape sequence a transcription forgot to decode. The escapes for
    /// <c>&lt;</c> and <c>&gt;</c> are the pair that actually happened, so the whole
    /// backslash-u-XXXX shape is refused rather than those two: a generic in one entry and a
    /// nested generic in the next fail the same way.
    /// </summary>
    [Fact]
    public void NoEntryCarriesAnUndecodedEscape()
    {
        var escaped = DeclaredEntries()
            .Where(entry => entry.Text.Contains(@"\u", StringComparison.Ordinal))
            .Select(entry => $"{entry.File}({entry.Number}): {entry.Text}")
            .ToList();

        Assert.True(
            escaped.Count == 0,
            "A declared API entry is the analyzer's own text, and these carry an escape it never "
            + "writes — decode it to the character it stands for:\n  " + string.Join("\n  ", escaped));
    }

    /// <summary>
    /// A generic's brackets balance. The escape above is how one goes missing, but a hand-edited
    /// entry loses one the same way, and the analyzer's message for either is the unhelpful pair.
    /// <para>
    /// Two things in an entry carry a bracket that is not a generic's, and both are counted out
    /// rather than waved through: the <c>-&gt;</c> every entry ends with, and a COMPARISON
    /// OPERATOR. <c>CodePosition</c> declares four of those and each is legitimately lopsided —
    /// this test found them on its first run, which is the point.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryGenericEntryBalancesItsBrackets()
    {
        var unbalanced = DeclaredEntries()
            .Where(entry =>
            {
                var text = OperatorToken.Replace(entry.Text, string.Empty);
                var opens = text.Count(c => c == '<');
                var closes = text.Count(c => c == '>') - Occurrences(text, "->");
                return opens != closes;
            })
            .Select(entry => $"{entry.File}({entry.Number}): {entry.Text}")
            .ToList();

        Assert.True(
            unbalanced.Count == 0,
            "These declared API entries open and close a different number of generic brackets:\n  "
            + string.Join("\n  ", unbalanced));
    }

    /// <summary>Longest first, so <c>&gt;&gt;</c> is never read as two <c>&gt;</c>.</summary>
    private static readonly Regex OperatorToken =
        new(@"\boperator\s*(>>>|<<|>>|<=|>=|<|>)", RegexOptions.Compiled);

    private static int Occurrences(string text, string token)
    {
        var count = 0;
        for (var i = text.IndexOf(token, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
