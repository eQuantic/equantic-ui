using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// A type riding behind another type's closing brace is invisible: to a reader who looks for it by
/// file name, and to any tool that moves code by member. <c>RealizedElement</c> — the web realizer's
/// only output shape — lived at the tail of a 2,666-line <c>WebRealizer.cs</c> and was DROPPED for
/// one build when that file was split by family, because the range check that drove the split
/// stopped at the last member of the class instead of at the end of the file. The compiler caught
/// it; the point is that it could go missing at all.
/// <para>
/// So the rule is one top-level type per file — and, because the tree says otherwise in 132 places,
/// the exceptions are a LIST rather than a category. A category the test waves through ("interop is
/// exempt") hides a careless second type inside it; a named entry with its reason stays visible and
/// stays reducible. The list may only shrink, and each entry carries the count it was measured at,
/// so an already-exempt file cannot quietly grow a third type either.
/// </para>
/// <para>
/// It lives in the COMPILER's tests because asking a C# file what types it declares is a question
/// only a parser answers honestly — a regex over source would be the kind of instrument this
/// repository replaces, and Roslyn is already here.
/// </para>
/// </summary>
public class OneTypePerFileTests
{
    private const string UpdateVariable = "EQ_UPDATE_ONE_TYPE_BASELINE";

    /// <summary>What a fresh entry says until somebody decides. It is one of the reasons on
    /// purpose: the honest record of "measured, not yet judged" beats a plausible sentence
    /// nobody checked.</summary>
    private const string Unjudged = "not looked at yet";

    [Fact]
    public void TheExceptionsToOneTypePerFileOnlyEverLeaveTheList()
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
            "A file declares more than one top-level type and is not in the baseline. Split it — the "
            + "second type gets its own file, named after it. If the file genuinely IS the unit (a "
            + "header transcribed from interop, a closed hierarchy, one protocol's record set), add "
            + "it to the baseline BY HAND with the count and the reason:\n  "
            + string.Join("\n  ", unlisted.Select(path => $"{path}  {measured[path]}  <reason>")));

        var grown = measured.Where(file => committed.TryGetValue(file.Key, out var entry) && file.Value > entry.Count)
            .OrderBy(file => file.Key, StringComparer.Ordinal).ToList();
        Assert.True(grown.Count == 0,
            "A file already in the baseline grew a type. Being listed is an exception for the types "
            + "that were measured, not a licence for the next one:\n  "
            + string.Join("\n  ", grown.Select(file =>
                $"{file.Key}  {committed[file.Key].Count} -> {file.Value}")));

        var stale = committed.Where(entry => !measured.TryGetValue(entry.Key, out var count) || count < entry.Value.Count)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0,
            $"{stale.Count} entry(ies) shrank or are gone — regenerate ({UpdateVariable}=1) so the "
            + "number moves on record. Regenerating is for a REMOVAL; it keeps every reason already "
            + "written and never invents one for a file that just gained a type:\n  "
            + string.Join("\n  ", stale.Select(entry =>
                $"{entry.Key}  {entry.Value.Count} -> {(measured.TryGetValue(entry.Key, out var now) ? now.ToString() : "gone")}")));

        var unexplained = committed.Where(entry => entry.Value.Reason.Length == 0)
            .Select(entry => entry.Key).OrderBy(path => path, StringComparer.Ordinal).ToList();
        Assert.True(unexplained.Count == 0,
            "A baseline entry with no reason is a category exemption wearing a path. Say what makes "
            + $"the file the unit, or \"{Unjudged}\" if nobody has decided yet:\n  "
            + string.Join("\n  ", unexplained));
    }

    /// <summary>
    /// How many top-level types each <c>src/</c> file declares, counting only the files that declare
    /// more than one. Partial halves of one type count ONCE — two <c>partial class Foo</c> blocks in
    /// a file are still one type, and the hazard this guards is a SECOND type, not a second block.
    /// </summary>
    private static Dictionary<string, int> Census()
    {
        var census = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs",
                     SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var unit = CSharpSyntaxTree.ParseText(File.ReadAllText(file),
                new CSharpParseOptions(LanguageVersion.Preview)).GetCompilationUnitRoot();
            var declared = TopLevelTypes(unit.Members).ToHashSet(StringComparer.Ordinal);
            if (declared.Count > 1)
                census[Path.GetRelativePath(RepoRoot(), file).Replace(Path.DirectorySeparatorChar, '/')] = declared.Count;
        }

        return census;
    }

    /// <summary>The types a compilation unit declares, through however many namespaces it opens —
    /// a nested type belongs to the type that owns it and is not one of these.</summary>
    private static IEnumerable<string> TopLevelTypes(IEnumerable<MemberDeclarationSyntax> members)
    {
        foreach (var member in members)
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax nested:
                    foreach (var name in TopLevelTypes(nested.Members)) yield return name;
                    break;
                case BaseTypeDeclarationSyntax type:
                    yield return type.Identifier.Text + '`' + Arity(type);
                    break;
                case DelegateDeclarationSyntax @delegate:
                    yield return @delegate.Identifier.Text + '`' + (@delegate.TypeParameterList?.Parameters.Count ?? 0);
                    break;
            }
    }

    private static int Arity(BaseTypeDeclarationSyntax type) =>
        type is TypeDeclarationSyntax generic ? generic.TypeParameterList?.Parameters.Count ?? 0 : 0;

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

    /// <summary>
    /// Rewrites the list from the measurement, carrying every reason already written across. A file
    /// that is newly over the line gets <see cref="Unjudged"/> and nothing more — the regenerator
    /// cannot write the sentence that justifies it, so it says so and leaves the author to.
    /// </summary>
    private static void Regenerate(Dictionary<string, int> measured,
        Dictionary<string, (int Count, string Reason)> committed)
    {
        var entries = measured.OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => $"{file.Key}  {file.Value}  "
                + (committed.TryGetValue(file.Key, out var entry) && entry.Reason.Length > 0
                    ? entry.Reason
                    : Unjudged));

        File.WriteAllText(BaselinePath(),
            string.Join("\n", File.ReadAllLines(BaselinePath()).TakeWhile(line => line.StartsWith('#')))
            + "\n" + string.Join("\n", entries) + "\n");
    }

    private static string BaselinePath() => Path.Combine(
        RepoRoot(), "tests", "eQuantic.UI.Compiler.Tests", "Coverage", "one-type-per-file.baseline.txt");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
