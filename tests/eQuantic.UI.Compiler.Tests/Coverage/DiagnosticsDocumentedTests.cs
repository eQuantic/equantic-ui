using System.Text.RegularExpressions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// Every code the compiler can print has a row in <c>docs/DIAGNOSTICS.md</c>, and every row names
/// a code that still exists. A diagnostic nobody can look up is barely a diagnostic: the codes are
/// scattered across the call sites that raise them, so nothing but this holds them together.
/// <para>
/// The second half — a code may not gain a reporting FILE without the baseline saying so — is not
/// tidiness. <c>EQ2101</c> was two unrelated errors for months, a resx translation mismatch and
/// <c>System.IO</c> in a client component, each with its own green test, because the two never met
/// in one compilation. Nothing could have noticed except a list of who reports what.
/// Regenerate with EQ_UPDATE_DIAGNOSTICS_BASELINE=1.
/// </para>
/// </summary>
public class DiagnosticsDocumentedTests
{
    // Three idioms raise a diagnostic: a code passed as its own argument ("EQ2007", …), one
    // written straight into an MSBuild-canonical line ($"…: error EQ1005: {message}"), and an
    // <Error Code="EQ4001"> in one of the SDK's own .targets/.props. Matching only the first
    // missed every diagnostic the build HOST raises, which is where EQ1005 lives; matching only
    // C# missed every diagnostic the BUILD raises before the compiler runs, which is where EQ4001
    // lives.
    private static readonly Regex CodeLiteral =
        new(@"""(EQ\d{4})""|(?:error|warning)\s+(EQ\d{4})|Code=""(EQ\d{4})""", RegexOptions.Compiled);
    private static readonly string[] ReportingFiles = ["*.cs", "*.targets", "*.props"];
    private static readonly Regex DocumentedRow = new(@"^\|\s*`(EQ\d{4})`", RegexOptions.Multiline);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    /// <summary>Code to the source files that report it, which is the thing a collision shows up in.</summary>
    private static SortedDictionary<string, SortedSet<string>> ReportedCodes()
    {
        var reported = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var file in ReportingFiles.SelectMany(pattern => Directory.EnumerateFiles(src, pattern, SearchOption.AllDirectories)))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;
            foreach (Match match in CodeLiteral.Matches(File.ReadAllText(file)))
            {
                var code = match.Groups.Cast<Group>().Skip(1).First(group => group.Success).Value;
                if (!reported.TryGetValue(code, out var files))
                    reported[code] = files = new SortedSet<string>(StringComparer.Ordinal);
                // The path RELATIVE to src, not the filename: src has seven Program.cs, so a
                // filename key would collapse the build host's diagnostics with the CLI's and a
                // code moving between them would slip past this guard unseen.
                files.Add(Path.GetRelativePath(src, file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        return reported;
    }

    private static IReadOnlySet<string> DocumentedCodes() =>
        DocumentedRow.Matches(File.ReadAllText(Path.Combine(RepoRoot(), "docs", "DIAGNOSTICS.md")))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void EveryDiagnosticTheCompilerRaises_HasARowInTheReference()
    {
        var undocumented = ReportedCodes().Keys.Except(DocumentedCodes()).ToList();

        Assert.True(undocumented.Count == 0,
            "these codes are raised by src/ and have no row in docs/DIAGNOSTICS.md — a build error "
            + "nobody can look up: " + string.Join(", ", undocumented));
    }

    [Fact]
    public void EveryRowInTheReference_NamesACodeThatStillExists()
    {
        // EQ0000 is the fallback for an error that arrived with no code, so it is documented and
        // never written as a literal beside a message.
        var raised = ReportedCodes().Keys.ToHashSet(StringComparer.Ordinal);
        var phantom = DocumentedCodes().Where(code => !raised.Contains(code) && code != "EQ0000").ToList();

        Assert.True(phantom.Count == 0,
            "docs/DIAGNOSTICS.md documents codes nothing raises any more: " + string.Join(", ", phantom));
    }

    [Fact]
    public void NoCodeGainsAReportingSite_WithoutTheBaselineSayingSo()
    {
        var report = string.Join("\n", ReportedCodes()
            .Select(entry => $"{entry.Key} {string.Join(" ", entry.Value)}")) + "\n";
        var baselinePath = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Compiler.Tests",
            "Coverage", "diagnostics.baseline.txt");

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_DIAGNOSTICS_BASELINE") == "1")
        {
            File.WriteAllText(baselinePath, report);
            return;
        }

        Assert.True(File.Exists(baselinePath),
            "No committed baseline — run once with EQ_UPDATE_DIAGNOSTICS_BASELINE=1 and commit the file.");
        var committed = File.ReadAllText(baselinePath).Replace("\r\n", "\n");

        Assert.True(committed == report,
            "who reports which diagnostic has changed. If a code gained a file, make sure it is the "
            + "SAME error — EQ2101 once meant two unrelated things because nobody checked — then "
            + "regenerate with EQ_UPDATE_DIAGNOSTICS_BASELINE=1.\n\nexpected:\n" + committed
            + "\nactual:\n" + report);
    }
}
