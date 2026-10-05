using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// A JavaScript function a lowering writes by HAND, as text, around the C# it translates: an arrow
/// invoked in place, a callback, a <c>function(…)</c>. Each one is where two defects live (#539). An
/// <c>await</c> in the C# lands in a function that is not <c>async</c>, and the module does not
/// parse; and the C# runs as often as the function does, once per element of a callback, where C#
/// evaluates it once. <c>checked</c>, a <c>throw</c> expression, <c>Trim</c>'s characters and
/// <c>Enumerable.Range</c> and <c>Repeat</c> were five such sites, and the fix for each was the same:
/// the C# goes in as an ARGUMENT, evaluated where C# evaluates it, to a runtime function or to a
/// template's bound part, and the function holds no C# of its own.
/// <para>
/// So the hand-written functions are a NUMBER per file, with a committed baseline that may only
/// shrink, as <see cref="OneTypePerFileTests"/> keeps its list. A new one fails here: write the C# as
/// an argument instead, or, where a function is the honest shape (a callback that holds no C#, the
/// IR's own writer), add the file BY HAND with its count and the reason. Each entry carries the
/// count it was measured at, so a listed file cannot quietly grow one more.
/// </para>
/// <para>
/// It reads the compiler's SOURCE with Roslyn and counts the string literals and the text of the
/// interpolated strings that introduce a function (<c>=&gt;</c>, <c>function(</c>): the IR's own nodes
/// (<c>JsExpr.Arrow</c>) are not text, and a C# lambda in the compiler is not a string.
/// </para>
/// </summary>
public class IntroducedFunctionsCoverageTests
{
    private const string UpdateVariable = "EQ_UPDATE_INTRODUCED_FUNCTIONS";

    /// <summary>What a fresh entry says until somebody decides: measured, not judged.</summary>
    private const string Unjudged = "not looked at yet";

    [Fact]
    public void TheHandWrittenFunctionsOfTheLoweringsOnlyEverLeaveTheList()
    {
        var measured = Census();
        var committed = Committed();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Regenerate(measured, committed);
            return;
        }

        var unlisted = measured.Keys.Where(path => !committed.ContainsKey(path))
            .OrderBy(path => path, StringComparer.Ordinal).ToList();
        Assert.True(unlisted.Count == 0,
            "A lowering writes a JavaScript function by hand, as text, and its file is not in the baseline. "
            + "Pass the C# in as an ARGUMENT instead (a runtime function, or a template's part, which the "
            + "writer binds once): a function around C# is where an await stops parsing and a value runs "
            + "per element (#539). If the function holds no C#, add the file BY HAND with the count and the "
            + "reason:\n  "
            + string.Join("\n  ", unlisted.Select(path => $"{path}  {measured[path]}  <reason>")));

        var grown = measured.Where(file => committed.TryGetValue(file.Key, out var entry) && file.Value > entry.Count)
            .OrderBy(file => file.Key, StringComparer.Ordinal).ToList();
        Assert.True(grown.Count == 0,
            "A file already in the baseline writes one more function by hand. Being listed is an exception "
            + "for the functions that were measured, not a licence for the next one:\n  "
            + string.Join("\n  ", grown.Select(file => $"{file.Key}  {committed[file.Key].Count} -> {file.Value}")));

        var stale = committed.Where(entry => !measured.TryGetValue(entry.Key, out var count) || count < entry.Value.Count)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0,
            $"{stale.Count} entry(ies) shrank or are gone: regenerate ({UpdateVariable}=1) so the number "
            + "moves on record:\n  "
            + string.Join("\n  ", stale.Select(entry =>
                $"{entry.Key}  {entry.Value.Count} -> {(measured.TryGetValue(entry.Key, out var now) ? now.ToString() : "gone")}")));

        var unexplained = committed.Where(entry => entry.Value.Reason.Length == 0)
            .Select(entry => entry.Key).OrderBy(path => path, StringComparer.Ordinal).ToList();
        Assert.True(unexplained.Count == 0,
            $"A baseline entry with no reason. Say why the function is the honest shape, or \"{Unjudged}\":\n  "
            + string.Join("\n  ", unexplained));
    }

    /// <summary>The literal text that introduces a function, per file of the compiler's CodeGen.</summary>
    private static Dictionary<string, int> Census()
    {
        var census = new Dictionary<string, int>(StringComparer.Ordinal);
        var codeGen = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Compiler", "CodeGen");
        foreach (var file in Directory.EnumerateFiles(codeGen, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file),
                new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
            var count = root.DescendantTokens().Count(token => IsText(token) && Introduces(token.ValueText));
            if (count > 0)
                census[Path.GetRelativePath(RepoRoot(), file).Replace(Path.DirectorySeparatorChar, '/')] = count;
        }

        return census;
    }

    private static bool IsText(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StringLiteralToken)
        || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
        || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
        || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken);

    private static bool Introduces(string text) =>
        text.Contains("=>", StringComparison.Ordinal)
        || text.Contains("function(", StringComparison.Ordinal)
        || text.Contains("function (", StringComparison.Ordinal);

    private static Dictionary<string, (int Count, string Reason)> Committed()
    {
        var committed = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(BaselinePath()))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var fields = line.Split("  ", 3, StringSplitOptions.TrimEntries);
            committed[fields[0]] = (int.Parse(fields[1]), fields.Length > 2 ? fields[2] : "");
        }

        return committed;
    }

    /// <summary>Rewrites the list from the measurement, carrying every reason across; a file new to
    /// the list gets <see cref="Unjudged"/>, which the diff is where somebody notices.</summary>
    private static void Regenerate(Dictionary<string, int> measured,
        Dictionary<string, (int Count, string Reason)> committed)
    {
        var entries = measured.OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => $"{file.Key}  {file.Value}  "
                + (committed.TryGetValue(file.Key, out var entry) && entry.Reason.Length > 0 ? entry.Reason : Unjudged));

        File.WriteAllText(BaselinePath(),
            string.Join("\n", File.ReadAllLines(BaselinePath()).TakeWhile(line => line.StartsWith('#')))
            + "\n" + string.Join("\n", entries) + "\n");
    }

    private static string BaselinePath() => Path.Combine(
        RepoRoot(), "tests", "eQuantic.UI.Compiler.Tests", "Coverage", "introduced-functions.baseline.txt");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
