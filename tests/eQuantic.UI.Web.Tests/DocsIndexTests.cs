using System.Globalization;
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
/// <para>
/// A citation into a <c>.cs</c> file carries a LINE too, and the line is the part that rots: the last
/// three tests read it. One refuses a line past the end of the file, which is what a SPLIT leaves
/// behind; one holds a fenced citation to the code it QUOTES; one holds a prose citation to the MEMBER
/// it names. The last two are the drift a line number alone cannot show — a component gaining ten lines
/// moves every citation below it, and on the day they were written 100 of the handoff audit's 290
/// fenced quotes had slid off their line across twenty files no split ever touched.
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

    /// <summary>
    /// A citation cannot point PAST THE END of the file it names.
    ///
    /// <para>
    /// The guard above reads only fenced evidence blocks in the one-line <c>path␣␣code</c> form.
    /// Citations in PROSE — <c>(PhotonRealizer.cs:1658-1665 ExpandHitRect)</c> — were unjudged, and
    /// S6 showed what that costs: moving 1,300 lines out of one file left TWENTY such citations
    /// pointing into a file that now ends at 343, and nothing went red. This is the cheapest rule
    /// that catches the whole class, needs no prose parsing, and cannot false-positive: if the file
    /// has fewer lines than the citation names, the citation is wrong, whatever it meant.
    /// </para>
    ///
    /// <para>
    /// WHAT IT CANNOT SEE: drift WITHIN the file's length. Half of those twenty were already wrong
    /// on <c>main</c> before the move — <c>ExpandHitRect</c> was cited at 1658 and declared at 1797
    /// — and a line that still exists is a line this test accepts. That half is closed by the two
    /// guards below, each reading the part of a citation that survives a move:
    /// <see cref="Every_quoted_line_is_at_the_line_its_citation_names"/> reads the QUOTE a fenced
    /// citation carries, and
    /// <see cref="Every_member_named_beside_a_citation_is_declared_where_the_citation_points"/> the
    /// MEMBER a prose one names. This test stays, because a citation can also point past a file that
    /// quotes nothing and names nobody.
    /// </para>
    /// </summary>
    [Fact]
    public void No_citation_points_past_the_end_of_the_file_it_names()
    {
        var root = Root();
        var sources = Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories))
            .ToLookup(Path.GetFileName, StringComparer.Ordinal);
        var lengths = new Dictionary<string, int>(StringComparer.Ordinal);

        int Length(string path) =>
            lengths.TryGetValue(path, out var known) ? known : lengths[path] = File.ReadAllLines(path).Length;

        var overshot = new List<string>();
        foreach (var file in RepositoryMarkdown(root))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                foreach (Match cited in CitedLine.Matches(line))
                {
                    var path = cited.Groups["path"].Value;
                    var rooted = path.Contains('/', StringComparison.Ordinal);
                    var candidates = (rooted ? [Path.Combine(root, path)] : sources[Path.GetFileName(path)].ToArray())
                        .Where(File.Exists).ToArray();
                    var where = $"{Path.GetRelativePath(root, file)}:{lineNumber}";
                    if (candidates.Length == 0)
                    {
                        // A BARE NAME that resolves to nothing mentions rather than locates. A ROOTED
                        // one names a path, and a path that is not there is already wrong.
                        if (rooted) overshot.Add($"{where} → {path} (no such file)");
                        continue;
                    }

                    var wanted = int.Parse(
                        cited.Groups["to"].Success ? cited.Groups["to"].Value : cited.Groups["from"].Value,
                        CultureInfo.InvariantCulture);
                    if (candidates.Any(candidate => Length(candidate) >= wanted)) continue;

                    overshot.Add($"{where} → {path}:{wanted}, "
                        + $"but the longest file of that name has {candidates.Max(Length)} lines");
                }
            }
        }

        var allowed = File.ReadAllLines(Path.Combine(root, "tests", "eQuantic.UI.Web.Tests", "Coverage",
                "citation-overshoot.baseline.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(p => p[0], p => int.Parse(p[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);

        var counted = overshot
            .GroupBy(entry => Path.GetFileName(entry.Split('\u2192')[1].Trim().Split(':', ' ')[0]),
                StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var grown = counted
            .Where(pair => !allowed.TryGetValue(pair.Key, out var was) || pair.Value > was)
            .Select(pair => allowed.ContainsKey(pair.Key)
                ? $"{pair.Key}: {pair.Value} citations overshoot, baselined at {allowed[pair.Key]}"
                : $"{pair.Key}: {pair.Value} citations overshoot, and it is not in the baseline at all")
            .Concat(allowed
                .Where(pair => counted.GetValueOrDefault(pair.Key) < pair.Value)
                .Select(pair => $"{pair.Key}: baselined at {pair.Value} but only "
                    + $"{counted.GetValueOrDefault(pair.Key)} remain — shrink the baseline, it may only go down"))
            .ToArray();

        string.Join(Environment.NewLine, grown).Should().BeEmpty(
            "a citation into a file shorter than the line it names cannot be read by anyone; this is what a "
            + "split leaves behind, and it is the half of citation rot a machine can settle without judgement. "
            + "The baseline is EMPTY — #207 repointed the ninety-five that five earlier slices left — "
            + "so a file appearing here is a citation nobody can follow, not a known debt");
    }

    /// <summary>
    /// A fenced citation QUOTES the code it points at, so the quote is the claim and the line is
    /// checkable against it: the quoted text must be found at the lines the citation names.
    ///
    /// <para>
    /// This is the half of citation rot the two guards above are documented as unable to see —
    /// drift WITHIN the file's length, where the line still exists and holds something else. It was
    /// not a theory: on the day this was written 100 of the audit's 290 fenced quotes had slid off
    /// their line, across twenty files that no split ever touched, because a component gaining ten
    /// lines moves every citation below it and nothing said so. Eighty-eight were repointed by
    /// searching for the quote, the rest were re-quoted because the code itself had changed.
    /// </para>
    ///
    /// <para>
    /// THE CONVENTION THE RULE RESTS ON, stated because it is now load-bearing: inside a fence a
    /// citation is followed by the CODE it names (two spaces, then the line, exactly as
    /// <see cref="EvidenceLine"/> already reads it); in prose it is followed by the MEMBER it names,
    /// which <see cref="Every_member_named_beside_a_citation_is_declared_where_the_citation_points"/>
    /// checks. A member name written inside a fence would eat the two-space seam and blind the
    /// declaration guard, so the two forms stay apart.
    /// </para>
    ///
    /// <para>
    /// WHAT IT DECLINES, rather than guesses at: a quote with an ELISION in its first forty
    /// characters (<c>...</c>) and a quote shorter than twelve — <c>{</c> and <c>}, Child);</c> are
    /// quoted to be read, not diffed, and would match anywhere. Four citations are declined today.
    /// The audits also ANNOTATE a quote (<c>   // native only</c>, <c>   (no minimum reaches it)</c>);
    /// the annotation is cut at the first run of three spaces, or at two before <c>/</c>, <c>→</c>,
    /// <c>—</c> or <c>(</c>, which is the separator they already use.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_quoted_line_is_at_the_line_its_citation_names()
    {
        var root = Root();
        var sources = SourceFiles(root);
        var stale = new List<string>();

        foreach (var file in RepositoryMarkdown(root))
        {
            var fenced = false;
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { fenced = !fenced; continue; }
                if (!fenced) continue;

                // A quote ends where the NEXT citation begins, whatever separates them: the audits
                // write `A.cs:1  code  → B.cs:2  code` on one line, and reading to end-of-line swept
                // the second citation into the first one's claim.
                var starts = CitedLine.Matches(line).Select(next => next.Index).ToArray();
                foreach (Match cited in EvidenceQuote.Matches(line))
                {
                    var from = cited.Index + cited.Length;
                    var to = starts.Where(start => start > cited.Index).DefaultIfEmpty(line.Length).Min();
                    var claim = Claim(line[from..to]);
                    if (claim is null) continue;

                    var path = cited.Groups["path"].Value;
                    var rooted = path.Contains('/', StringComparison.Ordinal);
                    var candidates = (rooted ? [Path.Combine(root, path)] : sources[Path.GetFileName(path)].ToArray())
                        .Where(File.Exists).ToArray();
                    var where = $"{Path.GetRelativePath(root, file)}:{lineNumber}";
                    if (candidates.Length == 0)
                    {
                        if (rooted) stale.Add($"{where} → {path} (no such file)");
                        continue;
                    }

                    var lo = int.Parse(cited.Groups["from"].Value, CultureInfo.InvariantCulture);
                    var hi = cited.Groups["to"].Success
                        ? int.Parse(cited.Groups["to"].Value, CultureInfo.InvariantCulture)
                        : lo;
                    if (candidates.Any(candidate => Spans(candidate, lo, hi).Contains(claim, StringComparison.Ordinal)))
                        continue;

                    stale.Add($"{where} → {path}:{lo}"
                        + (hi == lo ? "" : $"-{hi}") + $" no longer holds \"{claim}\"");
                }
            }
        }

        string.Join(Environment.NewLine, stale).Should().BeEmpty(
            "a fenced citation quotes the code it points at, so the quote settles the line without judgement: "
            + "search the repository for the quote and repoint the citation, or re-quote it when the code itself "
            + "changed — and when it changed enough that the row's CLAIM is spent, re-judge the row");
    }

    /// <summary>
    /// A prose citation that names a MEMBER says which member the line holds, and that is checkable
    /// where the line alone is not: the named member must be declared in the file the citation names,
    /// and the line must fall inside it.
    ///
    /// <para>
    /// The overshoot guard accepts any line within the file's length, so a member that moved down its
    /// own file takes its citations with it invisibly — <c>Tokens.cs:241 BaseMs</c> was declared at
    /// 259 and read as fine. Naming the member closes that, and the audit's own convention already
    /// wrote it that way (<c>EmitVisitor.Interaction.cs:114-122 ExpandHitRect</c>) for the twenty
    /// citations S6 repointed; #207 repointed the remaining ninety-five the same way.
    /// </para>
    ///
    /// <para>
    /// WHAT COUNTS AS A NAME, and why it is narrow: an identifier with TWO capitals
    /// (<c>LowerPressable</c>, <c>MeasureStack</c>) or a dotted pair. English does not write those,
    /// so a sentence that happens to continue with a capitalised word after a citation cannot be
    /// accused — which is what a single-capital rule would do to <c>The</c>, and what a
    /// "declared anywhere" rule would do to <c>Text</c>, a common noun that is also a type here.
    /// The cost is stated rather than hidden: a one-word member (<c>Visit</c>, <c>Walk</c>) cannot be
    /// named, and citations of those name the enclosing type instead.
    /// </para>
    ///
    /// <para>
    /// The span is the declaration's FULL span, so citing a member's doc comment is citing the
    /// member. An overloaded name passes if ANY of its declarations contains the line — which is the
    /// honest answer, since the citation named a member and not an overload.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_member_named_beside_a_citation_is_declared_where_the_citation_points()
    {
        var root = Root();
        var sources = SourceFiles(root);
        var misplaced = new List<string>();

        foreach (var file in RepositoryMarkdown(root))
        {
            var fenced = false;
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { fenced = !fenced; continue; }
                if (fenced) continue;

                foreach (Match cited in NamedMember.Matches(line))
                {
                    var token = cited.Groups["member"].Value;
                    if (!token.Contains('.', StringComparison.Ordinal) && !TwoCapitals.IsMatch(token)) continue;
                    var member = token.Split('.')[^1];

                    var path = cited.Groups["path"].Value;
                    var rooted = path.Contains('/', StringComparison.Ordinal);
                    var candidates = (rooted ? [Path.Combine(root, path)] : sources[Path.GetFileName(path)].ToArray())
                        .Where(File.Exists).ToArray();
                    if (candidates.Length == 0) continue; // the overshoot guard already reports a path that is not there

                    var line0 = int.Parse(cited.Groups["from"].Value, CultureInfo.InvariantCulture);
                    var where = $"{Path.GetRelativePath(root, file)}:{lineNumber}";
                    var declaring = candidates.Where(candidate => DeclarationSpans(candidate).ContainsKey(member)).ToArray();
                    if (declaring.Length == 0)
                    {
                        misplaced.Add($"{where} → {path} does not declare {member}");
                        continue;
                    }

                    if (declaring.Any(candidate => DeclarationSpans(candidate)[member]
                            .Any(span => line0 >= span.First && line0 <= span.Last)))
                        continue;

                    var declared = string.Join(", ", declaring
                        .SelectMany(candidate => DeclarationSpans(candidate)[member])
                        .Select(span => $"{span.First}-{span.Last}"));
                    misplaced.Add($"{where} → {path}:{line0} is outside {member}, which is at {declared}");
                }
            }
        }

        string.Join(Environment.NewLine, misplaced).Should().BeEmpty(
            "a prose citation names the member the line holds; the member is the part that survives a move, "
            + "so it decides where the citation points — repoint the line to the member, or name the member "
            + "that is actually there");
    }

    /// <summary>The repository's own C#: what a citation can name. Test sources included, because the audits cite the tests that pin a fix.</summary>
    private static ILookup<string, string> SourceFiles(string root) =>
        Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories))
            .ToLookup(file => Path.GetFileName(file)!, StringComparer.Ordinal);

    /// <summary>A fenced evidence citation: the path, the line or range, and the two-space seam before the quote.</summary>
    private static readonly Regex EvidenceQuote = new(
        @"(?<path>(?:[\w.]+/)*[\w.]+\.cs):(?<from>\d+)(?:-(?<to>\d+))?\s{2,}",
        RegexOptions.Compiled);

    /// <summary>A prose citation followed by ONE space and the member it names.</summary>
    private static readonly Regex NamedMember = new(
        @"(?<!\w)(?<path>(?:[\w.]+/)*[\w.]+\.cs):(?<from>\d+)(?:-(?<to>\d+))? (?<member>[A-Z][A-Za-z0-9]*(?:\.[A-Z][A-Za-z0-9]*)*)",
        RegexOptions.Compiled);

    private static readonly Regex TwoCapitals = new(@"[A-Z][A-Za-z0-9]*[A-Z]", RegexOptions.Compiled);

    /// <summary>The audits' own annotation seam: three spaces, or two before a comment, arrow, dash or parenthesis.</summary>
    private static readonly Regex Annotation = new(@" {3,}| {2,}(?=[/→—(])", RegexOptions.Compiled);

    /// <summary>
    /// What a quote CLAIMS about the line: its first forty characters, with the audits' flattening
    /// escape expanded, whitespace collapsed and the annotation cut. <c>null</c> when the quote is
    /// too short or elided to settle anything.
    /// </summary>
    private static string? Claim(string quoted)
    {
        var text = Annotation.Split(quoted)[0].Replace("\\n", " ", StringComparison.Ordinal);
        var claim = Whitespace.Replace(text, " ").Trim();
        if (claim.Length > 40) claim = claim[..40];
        return claim.Length < 12 || claim.Contains("...", StringComparison.Ordinal)
            || claim.Contains('…', StringComparison.Ordinal) ? null : claim;
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Lines <paramref name="from"/> to <paramref name="to"/> of a source file, joined and collapsed — a quote the audit flattened is read against the same shape.</summary>
    private static string Spans(string sourceFile, int from, int to)
    {
        var lines = SourceLines.GetOrAdd(sourceFile, static file => File.ReadAllLines(file));
        if (from > lines.Length) return string.Empty;
        return Whitespace.Replace(string.Join(' ', lines[(from - 1)..Math.Min(to, lines.Length)]), " ").Trim();
    }

    private static readonly ConcurrentDictionary<string, string[]> SourceLines = new(StringComparer.Ordinal);

    /// <summary>
    /// Every member a file declares, with the LINES it spans — the full span, so a member's doc
    /// comment belongs to it. Parsed, for the reason <see cref="Declares"/> gives: a declaration
    /// quoted inside a comment is trivia to a parser and a declaration to a regex.
    /// </summary>
    private static Dictionary<string, List<(int First, int Last)>> DeclarationSpans(string sourceFile) =>
        Spanned.GetOrAdd(sourceFile, static file =>
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var spans = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var names = node switch
                {
                    PropertyDeclarationSyntax property => [property.Identifier.Text],
                    EnumMemberDeclarationSyntax member => [member.Identifier.Text],
                    MethodDeclarationSyntax method => [method.Identifier.Text],
                    EventDeclarationSyntax @event => [@event.Identifier.Text],
                    BaseTypeDeclarationSyntax type => [type.Identifier.Text],
                    FieldDeclarationSyntax field =>
                        field.Declaration.Variables.Select(variable => variable.Identifier.Text),
                    _ => Enumerable.Empty<string>(),
                };
                var lines = node.SyntaxTree.GetLineSpan(node.FullSpan);
                foreach (var name in names)
                    (spans.TryGetValue(name, out var known) ? known : spans[name] = [])
                        .Add((lines.StartLinePosition.Line + 1, lines.EndLinePosition.Line + 1));
            }

            return spans;
        });

    private static readonly ConcurrentDictionary<string, Dictionary<string, List<(int First, int Last)>>> Spanned =
        new(StringComparer.Ordinal);

    /// <summary>
    /// A prose or fenced citation: a <c>.cs</c> path and the line, or RANGE, it points at. The
    /// upper bound is what gets checked — a range whose start exists and whose end does not is
    /// still a range nobody can read, and the audit writes ranges far more often than single lines.
    /// </summary>
    private static readonly Regex CitedLine =
        new(@"(?<path>[A-Za-z0-9_./-]+\.cs):(?<from>\d+)(?:-(?<to>\d+))?", RegexOptions.Compiled);
}
