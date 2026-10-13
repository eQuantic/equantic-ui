using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The wiki's Capabilities page says, per contract, what each native shell hands a component, and the
/// shells' own registrations say the same, in both languages.
///
/// <para>
/// The table answers "does my target have a camera", and nothing held it to the code. Android
/// registered no <c>ICamera</c> until #775 while <c>UseCamera</c> wrote its permission, and a page
/// claiming otherwise would have read as current. Every contract still owed under #774 changes one
/// cell of this table, so each cell is compared with the registration that decides it.
/// </para>
///
/// <para>
/// DERIVED, not listed. Every <c>Devices/*Capabilities.cs</c> under a <c>Native.Shell.*</c> project
/// is a shell, and every <c>TryAddSingleton&lt;IContract, …&gt;</c> in it is a registration, so a
/// fifth shell or a thirteenth contract fails here until the page has its column or its row.
/// </para>
///
/// <para>
/// A cell has three answers, because "registered" is not "realized": a shell may register an object
/// that always reports itself unavailable, so that a page written for a phone still constructs on a
/// desktop. That one is read from the implementation, whose <c>IsAvailable</c> is the literal
/// <c>false</c>. Whether a machine HAS the hardware is a run-time answer and is not in the table.
/// </para>
/// </summary>
public class WikiShellRealizationsTests
{
    private enum Answer { Realized, Unavailable, Null }

    private const string ShellPrefix = "eQuantic.UI.Native.Shell.";

    /// <summary>The comment that precedes the table, in every language: prose around it is free.</summary>
    private static readonly Regex Marker = new(@"<!--\s*eq:shell-realizations\s*-->", RegexOptions.Compiled);

    private static readonly string? Root = LocateRoot();

    [Fact]
    public void TheTableSaysWhatEachShellRegisters_InBothLanguages()
    {
        if (WikiClone.Absent()) return;
        var wiki = WikiClone.Location!;

        var registered = Registered(Path.Combine(Root!, "src"));
        registered.Should().NotBeEmpty("the native shells register their capabilities in Devices/*Capabilities.cs");
        var contracts = new SortedSet<string>(registered.Values.SelectMany(shell => shell.Keys), StringComparer.Ordinal);

        // Named rather than discovered: these two pages are the contract, and a rename that lost one
        // would otherwise leave the guard reading nothing and reporting agreement.
        (string Language, string Path, Dictionary<string, Answer> Words)[] pages =
        [
            ("English", Path.Combine(wiki, "Capabilities.md"),
                new() { ["✓"] = Answer.Realized, ["unavailable"] = Answer.Unavailable, ["`null`"] = Answer.Null }),
            ("pt-BR", Path.Combine(wiki, "locale", "pt-BR", "Capabilities-pt-BR.md"),
                new() { ["✓"] = Answer.Realized, ["indisponível"] = Answer.Unavailable, ["`null`"] = Answer.Null }),
        ];

        var offences = new List<string>();
        foreach (var (language, path, words) in pages)
        {
            if (!File.Exists(path))
            {
                offences.Add($"the {language} Capabilities page is missing from the wiki ({path})");
                continue;
            }
            if (Table(File.ReadAllText(path)) is not { } table)
            {
                offences.Add($"{language}: no table follows an eq:shell-realizations comment");
                continue;
            }

            var columns = table.Header.Skip(1).ToList();
            foreach (var shell in registered.Keys.Where(shell => !columns.Contains(shell, StringComparer.OrdinalIgnoreCase)))
                offences.Add($"{language}: the {shell} shell registers capabilities and has no column");
            foreach (var column in columns.Where(column => !registered.Keys.Contains(column, StringComparer.OrdinalIgnoreCase)))
                offences.Add($"{language}: a column for {column}, which is no shell with a Devices/*Capabilities.cs");

            var rows = table.Rows.ToDictionary(row => row[0].Trim('`'), StringComparer.Ordinal);
            foreach (var contract in contracts.Where(contract => !rows.ContainsKey(contract)))
                offences.Add($"{language}: {contract} is registered by a shell and has no row");
            foreach (var row in rows.Keys.Where(row => !contracts.Contains(row)))
                offences.Add($"{language}: a row for {row}, which no shell registers");

            foreach (var (contract, cells) in rows.Where(row => contracts.Contains(row.Key)))
            {
                for (var index = 0; index < columns.Count; index++)
                {
                    var shell = registered.Keys.FirstOrDefault(name => name.Equals(columns[index], StringComparison.OrdinalIgnoreCase));
                    if (shell is null) continue;
                    var cell = index + 1 < cells.Length ? cells[index + 1] : "";
                    var expected = registered[shell].GetValueOrDefault(contract, Answer.Null);
                    if (!words.TryGetValue(cell, out var said))
                        offences.Add($"{language}: {contract} on {shell} reads \"{cell}\", which is none of {string.Join(", ", words.Keys)}");
                    else if (said != expected)
                        offences.Add($"{language}: {contract} on {shell} reads {said}, and the shell's registration says {expected}");
                }
            }
        }

        // Joined rather than asserted as a collection, so one run names both languages' drift.
        string.Join("\n", offences).Should().BeEmpty(
            "the table is what tells a reader which target answers a contract: change the cell in both "
            + "pages in one commit, on the wiki branch named like this pull request's, which CI reads (#406). "
            + $"Read from {WikiClone.Describe()}");
    }

    /// <summary>Per shell, what it registers for each contract.</summary>
    private static SortedDictionary<string, Dictionary<string, Answer>> Registered(string src)
    {
        var declarations = Declarations(src);
        var shells = new SortedDictionary<string, Dictionary<string, Answer>>(StringComparer.Ordinal);
        foreach (var project in Directory.GetDirectories(src, ShellPrefix + "*"))
        {
            var devices = Path.Combine(project, "Devices");
            var files = Directory.Exists(devices) ? Directory.GetFiles(devices, "*Capabilities.cs") : [];
            if (files.Length == 0) continue; // code shared between shells, and no shell of its own

            var answers = new Dictionary<string, Answer>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var registrations = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot()
                    .DescendantNodes().OfType<GenericNameSyntax>()
                    .Where(name => name.Identifier.Text == "TryAddSingleton");
                foreach (var registration in registrations)
                {
                    var arguments = registration.TypeArgumentList.Arguments;
                    // One type argument is a factory, which builds something: realized.
                    var implementation = arguments.Count > 1 ? arguments[1].ToString() : null;
                    answers[arguments[0].ToString()] = implementation is not null && AlwaysUnavailable(declarations, implementation)
                        ? Answer.Unavailable
                        : Answer.Realized;
                }
            }
            shells[Path.GetFileName(project)[ShellPrefix.Length..]] = answers;
        }
        return shells;
    }

    /// <summary>Every class the shells and the vocabulary declare, by name: where an implementation
    /// named in a registration is looked up.</summary>
    private static Dictionary<string, ClassDeclarationSyntax> Declarations(string src)
    {
        var declarations = new Dictionary<string, ClassDeclarationSyntax>(StringComparer.Ordinal);
        var projects = Directory.GetDirectories(src, ShellPrefix + "*").Append(Path.Combine(src, "eQuantic.UI.Primitives"));
        foreach (var project in projects)
        {
            foreach (var file in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(project, file).Replace('\\', '/');
                if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal)) continue;
                var text = File.ReadAllText(file);
                if (!text.Contains("IsAvailable", StringComparison.Ordinal)) continue;
                foreach (var declaration in CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                    declarations.TryAdd(declaration.Identifier.Text, declaration);
            }
        }
        return declarations;
    }

    /// <summary>True when the implementation's <c>IsAvailable</c> is the literal <c>false</c>: an
    /// honest absence, registered so that a constructor still resolves.</summary>
    private static bool AlwaysUnavailable(Dictionary<string, ClassDeclarationSyntax> declarations, string implementation)
    {
        // A capability with no IsAvailable (storage, the clipboard) has nothing to report absent.
        if (!declarations.TryGetValue(implementation, out var declaration)) return false;
        return declaration.Members.OfType<PropertyDeclarationSyntax>()
            .Any(property => property.Identifier.Text == "IsAvailable"
                && property.ExpressionBody?.Expression is LiteralExpressionSyntax literal
                && literal.Kind() == SyntaxKind.FalseLiteralExpression);
    }

    /// <summary>The table after the marker: its header cells, and each row's cells.</summary>
    private static (string[] Header, List<string[]> Rows)? Table(string page)
    {
        var lines = page.Replace("\r\n", "\n").Split('\n');
        var marker = Array.FindIndex(lines, line => Marker.IsMatch(line));
        if (marker < 0) return null;

        var table = lines.Skip(marker + 1).TakeWhile(line => line.StartsWith('|')).Select(Cells).ToList();
        // A header, the separator under it, and at least one row.
        if (table.Count < 3) return null;
        return (table[0], table.Skip(2).ToList());
    }

    private static string[] Cells(string line) =>
        line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static string? LocateRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here?.FullName;
    }
}
