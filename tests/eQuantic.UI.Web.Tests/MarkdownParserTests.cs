using eQuantic.UI.Components;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The markdown parser's truth suite — and the C# half of its PARITY proof. The fixture document
/// and every expected shape literal are cross-pinned with <c>markdown-parser.spec.ts</c> on the
/// runtime side: one canonical document, one shape dumper mirrored line for line, identical
/// expected strings in both suites. The parser runs on every side of the framework (C# for SSR
/// and Photon, its eqc twin in the browser), so the two suites agreeing IS the guarantee that the
/// server's render and the client's re-render describe the same tree.
/// </summary>
public class MarkdownParserTests
{
    // Backticks live inside the document, so it is written as lines — the TS twin does the same.
    private static readonly string Fixture = string.Join("\n",
    [
        "# Título *forte*",
        "",
        "Intro paragraph with **bold**, *italic*, `code **not bold**`, a [link](https://eq.dev) and ![logo](https://eq.dev/logo.png).",
        "",
        "## Getting started",
        "",
        "- first item",
        "- second **item**",
        "  continues here",
        "  - nested",
        "1. one",
        "2. two",
        "",
        "> A quote with `pipes | inside`.",
        "",
        "```csharp",
        "var x = \"# not a heading | not a table\";",
        "```",
        "",
        "| Col A | Col B |",
        "| --- | --- |",
        "| `a | b` | plain |",
        "",
        "---",
        "",
        "## Getting started",
        "",
        "Done. <!-- a comment --> Kept.",
    ]);

    private static readonly string[] Expected =
    [
        "h1(título-forte) Título forte",
        "p Intro paragraph with **bold**, *italic*, `code **not bold**`, a [link](https://eq.dev) and [logo](https://eq.dev/logo.png).",
        "h2(getting-started) Getting started",
        "list •@0 first item; •@0 second **item** continues here; •@1 nested; 1.@0 one; 2.@0 two",
        "q A quote with `pipes | inside`.",
        "code(csharp) var x = \"# not a heading | not a table\";",
        "table head=Col A~Col B row0=`a | b`~plain",
        "hr",
        "h2(getting-started-2) Getting started",
        "p Done.  Kept.",
    ];

    private static string RunText(MarkdownRun run)
    {
        var t = run.Text;
        if (run.Code) t = "`" + t + "`";
        if (run.Italic) t = "*" + t + "*";
        if (run.Bold) t = "**" + t + "**";
        if (run.IsLink) t = "[" + t + "](" + run.Href + ")";
        return t;
    }

    private static string RunsText(List<MarkdownRun> runs) =>
        string.Concat(runs.Select(RunText));

    private static string Shape(MarkdownBlock b) => b.Kind switch
    {
        MarkdownBlockKind.Heading => $"h{b.Level}({b.Id}) {b.Text}",
        MarkdownBlockKind.Paragraph => "p " + RunsText(b.Runs),
        MarkdownBlockKind.Quote => "q " + RunsText(b.Runs),
        MarkdownBlockKind.Code => $"code({b.Lang}) " + b.Raw.Replace("\n", "⏎"),
        MarkdownBlockKind.Rule => "hr",
        MarkdownBlockKind.List => "list " + string.Join("; ",
            b.Items.Select(it => $"{it.Marker}@{it.Depth} {RunsText(it.Runs)}")),
        MarkdownBlockKind.Table => "table head=" + string.Join("~", b.Head.Select(c => RunsText(c.Runs)))
            + string.Concat(b.Rows.Select((r, n) =>
                $" row{n}=" + string.Join("~", r.Cells.Select(c => RunsText(c.Runs))))),
        _ => "?" + b.Kind,
    };

    [Fact]
    public void ParsesTheCanonicalDocument_ToThePinnedShapes()
    {
        MarkdownParser.Parse(Fixture).Select(Shape).Should().Equal(Expected);
    }

    // ---- Edges beyond the fixture (truth side only) ------------------------------------------

    [Fact]
    public void ACodeSpan_KeepsMarkdownSyntaxLiteral()
    {
        var runs = MarkdownParser.Inline("before `**still two stars**` after");
        RunsText(runs).Should().Be("before `**still two stars**` after");
        runs[1].Code.Should().BeTrue();
        runs[1].Bold.Should().BeFalse();
    }

    [Fact]
    public void ItalicClosesPastABoldPair()
    {
        // `*Since **0.2.0***` is italic wrapping bold — taking the first `*` as the close is the
        // bug that left `**` on the documentation site's pages.
        var runs = MarkdownParser.Inline("*Since **0.2.0***");
        runs.Should().HaveCount(2);
        runs[0].Text.Should().Be("Since ");
        runs[0].Italic.Should().BeTrue();
        runs[1].Text.Should().Be("0.2.0");
        runs[1].Bold.Should().BeTrue();
        runs[1].Italic.Should().BeTrue();
    }

    [Fact]
    public void LoneAsterisks_StayLiteral()
    {
        RunsText(MarkdownParser.Inline("a * b * c has no emphasis when it closes empty: * *"))
            .Should().Contain("* *", "an immediate close is prose, not italics");
        MarkdownParser.Inline("2 * 3 = 6")[0].Text.Should().Be("2 * 3 = 6");
    }

    [Fact]
    public void AHashWithoutASpace_IsProse_NotAHeading()
    {
        var blocks = MarkdownParser.Parse("#hashtag culture");
        blocks.Should().HaveCount(1);
        blocks[0].Kind.Should().Be(MarkdownBlockKind.Paragraph);
    }

    [Fact]
    public void AnUnclosedFence_ClaimsTheRestOfTheDocument()
    {
        var blocks = MarkdownParser.Parse("```js\nlet a = 1;\n# still code");
        blocks.Should().HaveCount(1);
        blocks[0].Kind.Should().Be(MarkdownBlockKind.Code);
        blocks[0].Raw.Should().Be("let a = 1;\n# still code");
    }

    [Fact]
    public void AnUnclosedComment_SwallowsLinesUntilItCloses()
    {
        // The hidden middle line leaves a BLANK behind, so the prose either side of the comment
        // stays two paragraphs — a comment removes text, it does not splice sentences together.
        var blocks = MarkdownParser.Parse("before <!-- opens\nstill hidden\ncloses --> after");
        blocks.Should().HaveCount(2);
        RunsText(blocks[0].Runs).Should().Be("before");
        RunsText(blocks[1].Runs).Should().Be("after");
    }

    [Fact]
    public void CrLfSources_ParseExactlyLikeLfSources()
    {
        var lf = MarkdownParser.Parse("# A\n\ntext");
        var crlf = MarkdownParser.Parse("# A\r\n\r\ntext");
        crlf.Select(Shape).Should().Equal(lf.Select(Shape));
    }

    [Fact]
    public void AnImage_ReadsAsItsAltText_LinkingToTheSource()
    {
        // v1 fence: an Image node is an explicitly sized slot by spec, and markdown carries no
        // dimensions — so the picture arrives as a link the reader can open.
        var runs = MarkdownParser.Inline("![diagram](https://eq.dev/d.png)");
        runs.Should().HaveCount(1);
        runs[0].Text.Should().Be("diagram");
        runs[0].Href.Should().Be("https://eq.dev/d.png");
    }

    [Fact]
    public void AnEmptyAltImage_FallsBackToTheSourceAsItsText()
    {
        var runs = MarkdownParser.Inline("![](https://eq.dev/d.png)");
        runs.Should().HaveCount(1);
        runs[0].Text.Should().Be("https://eq.dev/d.png");
    }

    [Fact]
    public void EmptyAndNullSources_ParseToNoBlocks()
    {
        MarkdownParser.Parse("").Should().BeEmpty();
        MarkdownParser.Parse("   \n  \n").Should().BeEmpty();
    }
}
