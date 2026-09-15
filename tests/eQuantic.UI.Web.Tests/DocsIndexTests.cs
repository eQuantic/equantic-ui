using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <c>docs/README.md</c> is the index of <c>docs/</c>: every document at the top of the folder has a
/// row in it, and every local link in every document there resolves to something that exists.
/// <para>
/// Both failures were real on 2026-09-15. Nine finished plans were retired into <c>LEDGER.md</c>,
/// and three other documents cited them by path — a citation that still reads well and points at
/// nothing. And a document can be added without a row, where a reader never finds it. A move stales
/// every citation of the old path; this is the instrument that says so, instead of a grep someone
/// remembers to run.
/// </para>
/// </summary>
public class DocsIndexTests
{
    /// <summary>A Markdown link whose target is a path, not a URL, a fragment or a mailbox.</summary>
    private static readonly Regex LocalLink =
        new(@"\]\(((?!https?://|#|mailto:)[^)\s]+)\)", RegexOptions.Compiled);

    private static string Docs()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository, whose root holds src/eQuantic.UI.Runtime");
        return Path.Combine(here!.FullName, "docs");
    }

    private static IEnumerable<string> Targets(string markdown) =>
        LocalLink.Matches(markdown).Select(m => m.Groups[1].Value.Split('#')[0]).Where(t => t.Length > 0);

    [Fact]
    public void Every_document_at_the_top_of_docs_has_a_row_in_the_index()
    {
        var docs = Docs();
        var indexed = Targets(File.ReadAllText(Path.Combine(docs, "README.md"))).ToHashSet(StringComparer.Ordinal);

        var unindexed = Directory.GetFiles(docs, "*.md")
            .Select(Path.GetFileName)
            .Where(name => name != "README.md" && !indexed.Contains(name!))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        unindexed.Should().BeEmpty(
            "every document at the top of docs/ is a row of docs/README.md — add the row, or retire the document into LEDGER.md");
    }

    [Fact]
    public void Every_local_link_in_docs_resolves()
    {
        var docs = Docs();
        var dangling = new List<string>();
        foreach (var file in Directory.GetFiles(docs, "*.md").OrderBy(f => f, StringComparer.Ordinal))
        {
            foreach (var target in Targets(File.ReadAllText(file)).Distinct())
            {
                var path = Path.Combine(docs, target);
                if (!File.Exists(path) && !Directory.Exists(path))
                    dangling.Add($"{Path.GetFileName(file)} → {target}");
            }
        }

        dangling.Should().BeEmpty(
            "a link in docs/ names a path that is not there; a retired or moved document takes its citations with it");
    }
}
