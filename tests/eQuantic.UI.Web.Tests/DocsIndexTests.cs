using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
/// <para>
/// A path that RESOLVES can still be the wrong file, which is the failure the first two tests cannot
/// see. When <c>VisualNode.cs</c> became fifty-nine files (#162), fifty-five citations were rewritten
/// to name the file holding each member — and eleven named a file that exists and does not hold it
/// (<c>ThresholdDp</c> sent to <c>VisualNode.cs</c>, <c>SlideUp</c> to <c>BoxStyle.cs</c>, the gradient
/// axis to <c>LinearGradient.cs</c> rather than <c>GradientDirection.cs</c>). Every one passed the
/// resolution test above. The third test reads the evidence blocks instead: where an audit quotes a
/// DECLARATION beside a path, the file it names must declare that identifier.
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

    /// <summary>
    /// An evidence line of the audits: a source path, two or more spaces, then the line of code it
    /// quotes — <c>src/…/DragDismiss.cs    public const float ThresholdDp = 96;</c>.
    /// </summary>
    private static readonly Regex EvidenceLine = new(
        @"^\s{0,6}(?<path>(?:[\w.]+/)*[\w.]+\.cs)(?<at>:\d+(?:-\d+)?)?\s{2,}(?<code>\S.*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// The three shapes whose DECLARED NAME can be read out of a quoted line without parsing C#: a
    /// property, a const or readonly field, and an enum member. Everything else the audits quote is a
    /// call, a fragment, or a paraphrase with an elision in it — the audits quote to be READ, not to
    /// be diffed, so a line this cannot name is one the instrument declines to judge rather than one
    /// it fails. Narrow and certain beats wide and noisy: measured over the two audits, these three
    /// find every wrong file and accuse none of the seventy-eight paraphrased lines.
    /// </summary>
    private static readonly Regex[] DeclaredName =
    [
        new(@"(?<name>[A-Z]\w*)\s*\{\s*get", RegexOptions.Compiled),
        new(@"\b(?:const|readonly)\s+[\w?<>\[\].]+\s+(?<name>[A-Z]\w*)\s*=", RegexOptions.Compiled),
        new(@"^(?<name>[A-Z]\w*)\s*=\s*-?\d+\s*,?$", RegexOptions.Compiled),
    ];

    /// <summary>
    /// The names a quoted line declares — ONE, because a line that quotes several at once is
    /// declined rather than guessed at, and none at all when the line is an ASSIGNMENT that reads
    /// like one.
    /// <para>
    /// <c>Name = 0,</c> is an enum member or an object initializer's assignment and NOTHING in the
    /// text tells them apart. That is why reading every comma-separated assignment was rejected
    /// (it accused <c>BottomSheet.cs   Width = 32, Height = 4,</c>), and the single-name form had
    /// the identical defect one line further down: <c>AppBar.cs:72   Height = 56,</c> sets a
    /// <c>BoxStyle</c> property that <c>AppBar</c> does not declare. Seven citations were being
    /// judged that way and passing only because the old line-based source scan made the SAME
    /// mistake symmetrically — two wrong readings cancelling, which is the shape of defect this PR
    /// found in the layout lists.
    /// </para>
    /// <para>
    /// What tells them apart is the citation, not the code: <paramref name="located"/> is true when
    /// it carried a <c>:line</c>, and then it points at a LINE OF CODE — a claim this instrument
    /// cannot check, since the number is the part that rots. Without one it names the file that
    /// HOLDS the member, which is exactly the claim being checked. So the assignment form is read
    /// only there. A property or a <c>const</c> is a declaration wherever it is quoted and is read
    /// either way.
    /// </para>
    /// </summary>
    private static IEnumerable<string> QuotedDeclarations(string code, bool located)
    {
        foreach (var pattern in DeclaredName)
        {
            if (pattern.Match(code) is not { Success: true } match) continue;
            if (located && ReferenceEquals(pattern, DeclaredName[^1])) return [];
            return [match.Groups["name"].Value];
        }

        return [];
    }

    /// <summary>
    /// DECLARES it, rather than mentions it — and PARSED, rather than read line by line.
    /// <para>
    /// Two holes, found one after the other, and both were the same hole on opposite sides. The
    /// first version asked whether the file CONTAINED the word, which a doc comment satisfies:
    /// <c>Presence.cs</c> says "The SlideUp rise distance" above <c>SlideDistance</c> and declares
    /// no <c>SlideUp</c>. Matching the declaration SHAPES against raw lines fixed that and kept the
    /// hole: <c>ComponentDefinition.cs</c> carries the XML-doc example
    /// <c>public Matrix2D Placement { get; init; }</c>, which reads as a property declaration to any
    /// regex, so a citation of <c>Placement</c> moved there would have stayed green.
    /// </para>
    /// <para>
    /// A comment is TRIVIA to a parser and can never be a declaration, so the class of defect closes
    /// rather than narrowing again. The asymmetry with the quoted line is deliberate and is not a
    /// second hole: the audit quotes a FRAGMENT, with elisions and annotations inside it, which no
    /// parser accepts — so the quote is read by shape and the source is read by parse, each by what
    /// it actually is.
    /// </para>
    /// </summary>
    private static bool Declares(string sourceFile, string name) =>
        Declarations.GetOrAdd(sourceFile, static file =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().DescendantNodes()
                .SelectMany(node => node switch
                {
                    PropertyDeclarationSyntax property => [property.Identifier.Text],
                    EnumMemberDeclarationSyntax member => [member.Identifier.Text],
                    MethodDeclarationSyntax method => [method.Identifier.Text],
                    EventDeclarationSyntax @event => [@event.Identifier.Text],
                    BaseTypeDeclarationSyntax type => [type.Identifier.Text],
                    FieldDeclarationSyntax field =>
                        field.Declaration.Variables.Select(variable => variable.Identifier.Text),
                    _ => Enumerable.Empty<string>(),
                })
                .ToHashSet(StringComparer.Ordinal))
            .Contains(name);

    /// <summary>One parse per file: the audits cite the same handful many times over.</summary>
    private static readonly ConcurrentDictionary<string, HashSet<string>> Declarations = new(StringComparer.Ordinal);

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

    /// <summary>
    /// A citation that quotes a declaration is checkable: the file it names either declares that
    /// identifier or is the wrong file. A bare file name that resolves to nothing is a MENTION and is
    /// skipped, exactly as the backtick rule above skips <c>`LEDGER.md`</c> — a rooted path that
    /// resolves to nothing is a citation, and is reported.
    /// <para>
    /// WHAT IT CANNOT SEE, stated rather than implied, because a guard whose reach is guessed at is
    /// how three of these holes got here:
    /// </para>
    /// <list type="bullet">
    /// <item>Which of TWO members of the same name is meant. Sending <c>SlideUp</c> or
    /// <c>ThresholdDp</c> back to <c>VisualNode.cs</c> fails the test; sending
    /// <c>BoxStyle.Gradient</c> back to <c>Text.cs</c> does not, because <c>Text</c> has a
    /// <c>Gradient</c> of its own.</item>
    /// <item>An evidence block that puts the PATH on its own line and the quoted code beneath it.
    /// Only the one-line <c>path␣␣code</c> form is read, so those blocks are unjudged — found by
    /// mutating one and watching the test stay green.</item>
    /// <item>A citation carrying a <c>:line</c> whose quoted code is an assignment; see
    /// <see cref="QuotedDeclarations"/> for why that form is read only without one.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Every_quoted_declaration_is_declared_by_the_file_its_citation_names()
    {
        var root = Root();
        var sources = Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .ToLookup(Path.GetFileName, StringComparer.Ordinal);

        var misplaced = new List<string>();
        foreach (var file in RepositoryMarkdown(root))
        {
            var fenced = false;
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { fenced = !fenced; continue; }
                if (!fenced) continue;

                var cited = EvidenceLine.Match(line);
                if (!cited.Success) continue;

                var declarations = QuotedDeclarations(
                    cited.Groups["code"].Value, cited.Groups["at"].Success).ToArray();
                if (declarations.Length == 0) continue;

                var path = cited.Groups["path"].Value;
                var rooted = path.Contains('/', StringComparison.Ordinal);
                var candidates = (rooted ? [Path.Combine(root, path)] : sources[path].ToArray())
                    .Where(File.Exists).ToArray();

                var where = $"{Path.GetRelativePath(root, file)}:{lineNumber}";
                if (candidates.Length == 0)
                {
                    if (rooted)
                        misplaced.Add($"{where} → {path} (no such file) quoting {string.Join(", ", declarations)}");
                    continue; // a bare name that resolves to nothing mentions rather than locates
                }

                foreach (var name in declarations)
                    if (!candidates.Any(candidate => Declares(candidate, name)))
                        misplaced.Add($"{where} → {path} does not declare {name}");
            }
        }

        string.Join(Environment.NewLine, misplaced).Should().BeEmpty(
            "an audit quotes a declaration beside the file it says declares it; a file that resolves can still be "
            + "the wrong one, and a split moves the member without moving the citation");
    }
}
