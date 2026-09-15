using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <c>docs/README.md</c> is the index of <c>docs/</c>: every document at the top of the folder has a
/// row in it. And every citation of a Markdown file by PATH — a link, or a path in backticks — in
/// the repository's own Markdown (the root, <c>.github/</c>, <c>docs/</c>) resolves to something
/// that exists.
/// <para>
/// Both failures were real on 2026-09-15. Nine finished plans were retired into <c>LEDGER.md</c>;
/// the index kept nine rows pointing at them, three documents in <c>docs/</c> cited them, and the
/// root <c>ROADMAP.md</c> cited three of them in backticks — a citation that still reads well and
/// points at nothing, which a grep run by hand had missed. A move stales every citation of the old
/// path; this is the instrument that says so.
/// </para>
/// <para>
/// A path locates; a bare file name mentions. <c>`docs/LEDGER.md`</c> is checked, <c>`LEDGER.md`</c>
/// in prose is not — the ledger's own table of retired documents names them, and must be allowed to.
/// </para>
/// </summary>
public class DocsIndexTests
{
    /// <summary>
    /// An inline Markdown link's destination: <c>[text](path)</c>, <c>[text](path "title")</c> or
    /// <c>[text](&lt;path with spaces&gt;)</c>; not a URL, a fragment or a mailbox. Reference-style
    /// links resolve through their definition, which <see cref="ReferenceDefinition"/> reads.
    /// </summary>
    private static readonly Regex LinkDestination = new(
        @"\]\(\s*(?:<(?<angle>[^>]*)>|(?<bare>(?!https?://|#|mailto:)[^)\s]+))(?:\s+""[^""]*""|\s+'[^']*')?\s*\)",
        RegexOptions.Compiled);

    /// <summary>
    /// A reference-style link's definition, <c>[id]: path</c> or <c>[id]: &lt;path&gt;</c>, at the start of a
    /// line; every <c>[text][id]</c> in the file points here, so the definition is the link to check.
    /// </summary>
    private static readonly Regex ReferenceDefinition = new(
        @"^ {0,3}\[[^\]]+\]:\s*(?:<(?<angle>[^>]*)>|(?<bare>\S+))",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>A path to a Markdown file cited in backticks — it has a separator, so it locates rather than names.</summary>
    private static readonly Regex BacktickPath = new(
        @"`(?<path>[^`\s<>]*/[^`\s<>]*\.md)(?:[:#][^`]*)?`",
        RegexOptions.Compiled);

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository, whose root holds src/eQuantic.UI.Runtime");
        return here!.FullName;
    }

    /// <summary>The repository's own Markdown: the root, <c>.github/</c> and <c>docs/</c>, recursively.</summary>
    private static IEnumerable<string> RepositoryMarkdown(string root) =>
        Directory.GetFiles(root, "*.md")
            .Concat(Directory.GetFiles(Path.Combine(root, ".github"), "*.md", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static IEnumerable<string> LinkTargets(string markdown) =>
        LinkDestination.Matches(markdown).Concat(ReferenceDefinition.Matches(markdown))
            .Select(m => m.Groups["angle"].Success ? m.Groups["angle"].Value : m.Groups["bare"].Value)
            .Where(t => !Regex.IsMatch(t, @"^(https?://|mailto:|#)"))
            .Select(t => t.Split('#')[0])
            .Where(t => t.Length > 0);

    private static IEnumerable<string> Citations(string markdown) =>
        LinkTargets(markdown)
            .Concat(BacktickPath.Matches(markdown).Select(m => m.Groups["path"].Value))
            .Distinct(StringComparer.Ordinal);

    /// <summary>Relative to the citing file, or to the repository root — `docs/X.md` is written from inside docs/ too.</summary>
    private static bool Resolves(string citation, string fileDirectory, string root) =>
        new[] { Path.Combine(fileDirectory, citation), Path.Combine(root, citation) }
            .Any(p => File.Exists(p) || Directory.Exists(p));

    [Fact]
    public void Every_document_at_the_top_of_docs_has_a_row_in_the_index()
    {
        var docs = Path.Combine(Root(), "docs");
        var indexed = LinkTargets(File.ReadAllText(Path.Combine(docs, "README.md"))).ToHashSet(StringComparer.Ordinal);

        var unindexed = Directory.GetFiles(docs, "*.md")
            .Select(Path.GetFileName)
            .Where(name => name != "README.md" && !indexed.Contains(name!))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Joined, so the failure names every one of them rather than the first.
        string.Join(Environment.NewLine, unindexed).Should().BeEmpty(
            "every document at the top of docs/ is a row of docs/README.md — add the row, or retire the document into LEDGER.md");
    }

    [Fact]
    public void Every_path_citation_of_a_markdown_file_resolves()
    {
        var root = Root();
        var dangling = new List<string>();
        foreach (var file in RepositoryMarkdown(root))
        {
            var directory = Path.GetDirectoryName(file)!;
            foreach (var citation in Citations(File.ReadAllText(file)))
            {
                if (!Resolves(citation, directory, root))
                    dangling.Add($"{Path.GetRelativePath(root, file)} → {citation}");
            }
        }

        string.Join(Environment.NewLine, dangling).Should().BeEmpty(
            "a link or a backtick path in the repository's Markdown names a file that is not there; a retired or moved document takes its citations with it");
    }
}
